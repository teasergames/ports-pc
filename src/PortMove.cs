using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace Nexus {
  public sealed class MovePlan {
    public Game Game;
    public string TargetParent;
    // Installation folder first, then the update backups that belong to it: current path → new path.
    public List<KeyValuePair<string,string>> Folders = new List<KeyValuePair<string,string>>();
    public List<string> Files = new List<string>();
    public long Bytes, BackupBytes;
    public bool SameVolume;
    internal readonly List<KeyValuePair<string,string>> Done = new List<KeyValuePair<string,string>>();
  }

  // Moves a catalog-managed installation (and optionally its update backups) to another folder.
  // Same drive: the folders are renamed. Other drive: they are copied and verified first; the caller
  // saves the library and only then Finish deletes the old copy, so a failure never loses the port.
  public static class PortMove {
    static string Full(string path) { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
    static string Display(string p) { return p.StartsWith(@"\\?\UNC\") ? @"\\" + p.Substring(8) : p.StartsWith(@"\\?\") ? p.Substring(4) : p; }

    public static bool Supports(Game game) { return game != null && game.InstallationKind != "Installer" && UpdateManager.Managed(game); }

    public static IEnumerable<string> Backups(Game game) {
      string root = Full(game.ManagedInstallationRoot), prefix = Path.GetFileName(root) + ".nexus-backup-";
      foreach (string folder in Directory.GetDirectories(Path.GetDirectoryName(root), prefix + "*")) {
        Guid id;
        if (Guid.TryParseExact(Path.GetFileName(folder).Substring(prefix.Length), "N", out id) && PortRemoval.ReceiptMatches(folder, game.Repository)) yield return Full(folder);
      }
    }

    public static MovePlan Build(Game game, IEnumerable<Game> library, string targetParent, bool includeBackups) {
      if (!Supports(game)) throw new IOException("Só instalações feitas pelo catálogo do NEXUS podem ser movidas. Para outros ports, mova a pasta e atualize o caminho em Editar cadastro.");
      if (String.IsNullOrWhiteSpace(targetParent) || !Path.IsPathRooted(targetParent)) throw new IOException("Escolha uma pasta com caminho completo.");
      string source = Full(game.ManagedInstallationRoot), parent = Full(targetParent);
      if (parent.Equals(Full(Path.GetDirectoryName(source)), StringComparison.OrdinalIgnoreCase)) throw new IOException("O port já está nesta pasta.");
      if (PortRemoval.Within(source, parent)) throw new IOException("A nova pasta não pode ficar dentro da instalação atual.");
      foreach (var other in library.Where(g => g.Id != game.Id))
        if (new[] { other.Executable, other.ConfigurationExecutable, other.WorkingDirectory, other.ManagedInstallationRoot }.Any(p => !String.IsNullOrWhiteSpace(p) && PortRemoval.Within(source, p)))
          throw new IOException("A pasta também é usada por outro cadastro: " + other.Name + ".");

      var plan = new MovePlan { Game = game.Copy(), TargetParent = parent };
      plan.Folders.Add(new KeyValuePair<string,string>(source, Path.Combine(parent, Path.GetFileName(source))));
      if (includeBackups) foreach (string backup in Backups(game)) plan.Folders.Add(new KeyValuePair<string,string>(backup, Path.Combine(parent, Path.GetFileName(backup))));
      foreach (var folder in plan.Folders) {
        if (Directory.Exists(PortCatalog.FsPath(folder.Value)) || File.Exists(PortCatalog.FsPath(folder.Value))) throw new IOException("Já existe " + Path.GetFileName(folder.Value) + " na pasta escolhida. Nada foi movido.");
        long bytes = Scan(folder.Key, plan.Files);
        if (folder.Key == source) plan.Bytes += bytes; else plan.BackupBytes += bytes;
      }
      plan.SameVolume = String.Equals(Path.GetPathRoot(source), Path.GetPathRoot(parent), StringComparison.OrdinalIgnoreCase);
      if (!plan.SameVolume) {
        Directory.CreateDirectory(PortCatalog.FsPath(parent));
        var drive = new DriveInfo(Path.GetPathRoot(parent));
        if (drive.AvailableFreeSpace < plan.Bytes + plan.BackupBytes + 256L * 1024 * 1024) throw new IOException("Espaço insuficiente na unidade de destino.");
      }
      return plan;
    }

    // Lists files and rejects links or mount points, which a copy could follow outside the port.
    static long Scan(string root, List<string> files) {
      long total = 0; var pending = new Stack<string>(); pending.Push(root);
      while (pending.Count > 0) {
        string dir = pending.Pop();
        if ((File.GetAttributes(PortCatalog.FsPath(dir)) & FileAttributes.ReparsePoint) != 0) throw new IOException("A instalação contém links ou pontos de montagem. Nada foi movido.");
        foreach (string file in Directory.GetFiles(PortCatalog.FsPath(dir))) {
          if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException("A instalação contém links. Nada foi movido.");
          files.Add(Display(file)); total += new FileInfo(file).Length;
        }
        foreach (string child in Directory.GetDirectories(PortCatalog.FsPath(dir))) pending.Push(Display(child));
      }
      return total;
    }

    // Moves or copies every folder and returns the library entry with its paths rewritten.
    public static Game Execute(MovePlan plan, IProgress<InstallProgress> progress, CancellationToken token) {
      PortRemoval.EnsureClosed(plan.Folders.Select(f => f.Key), plan.Files);
      Directory.CreateDirectory(PortCatalog.FsPath(plan.TargetParent));
      try {
        long copied = 0;
        foreach (var folder in plan.Folders) {
          token.ThrowIfCancellationRequested();
          if (plan.SameVolume) { Directory.Move(PortCatalog.FsPath(folder.Key), PortCatalog.FsPath(folder.Value)); }
          else {
            string staging = Path.Combine(plan.TargetParent, ".nexus-move-" + Guid.NewGuid().ToString("N"));
            try { CopyTree(folder.Key, staging, plan, ref copied, progress, token); Directory.Move(PortCatalog.FsPath(staging), PortCatalog.FsPath(folder.Value)); }
            catch { if (Directory.Exists(PortCatalog.FsPath(staging))) Directory.Delete(PortCatalog.FsPath(staging), true); throw; }
          }
          plan.Done.Add(folder);
        }
      } catch { Abort(plan); throw; }
      if (progress != null) progress.Report(new InstallProgress { Message = "Atualizando o cadastro…", Percent = 99 });
      var moved = plan.Game.Copy();
      moved.Executable = Map(moved.Executable, plan); moved.ConfigurationExecutable = Map(moved.ConfigurationExecutable, plan);
      moved.WorkingDirectory = Map(moved.WorkingDirectory, plan); moved.ManagedInstallationRoot = Map(moved.ManagedInstallationRoot, plan);
      moved.SourceGameDirectory = Map(moved.SourceGameDirectory, plan); moved.GameContentSource = Map(moved.GameContentSource, plan);
      moved.UpdateBackupDirectory = Map(moved.UpdateBackupDirectory, plan);
      return moved;
    }

    static void CopyTree(string from, string to, MovePlan plan, ref long copied, IProgress<InstallProgress> progress, CancellationToken token) {
      long total = Math.Max(1, plan.Bytes + plan.BackupBytes);
      foreach (string file in plan.Files.Where(f => PortRemoval.Within(from, f))) {
        token.ThrowIfCancellationRequested();
        string target = PortCatalog.ChildPath(to, PackageSupport.Relative(from, file));
        Directory.CreateDirectory(PortCatalog.FsPath(Path.GetDirectoryName(target)));
        File.Copy(PortCatalog.FsPath(file), PortCatalog.FsPath(target), false);
        long length = new FileInfo(PortCatalog.FsPath(file)).Length;
        if (new FileInfo(PortCatalog.FsPath(target)).Length != length) throw new IOException("A cópia de " + Path.GetFileName(file) + " não confere. Nada foi movido.");
        copied += length;
        if (progress != null) progress.Report(new InstallProgress { Message = "Copiando " + (copied / 1073741824.0).ToString("0.00") + " / " + (total / 1073741824.0).ToString("0.00") + " GB…", Percent = copied * 98.0 / total });
      }
      // Empty folders (saves, caches) are kept as well.
      Directory.CreateDirectory(PortCatalog.FsPath(to));
      foreach (string dir in Directory.GetDirectories(PortCatalog.FsPath(from), "*", SearchOption.AllDirectories))
        Directory.CreateDirectory(PortCatalog.FsPath(PortCatalog.ChildPath(to, PackageSupport.Relative(from, Display(dir)))));
    }

    static string Map(string path, MovePlan plan) {
      if (String.IsNullOrEmpty(path)) return path;
      foreach (var folder in plan.Folders) {
        if (!PortRemoval.Within(folder.Key, path)) continue;
        string full = Full(path);
        return full.Length == folder.Key.Length ? folder.Value : folder.Value + full.Substring(folder.Key.Length);
      }
      return path;
    }

    // Undoes Execute: renames folders back (same drive) or deletes the new copies (other drive).
    public static void Abort(MovePlan plan) {
      foreach (var folder in Enumerable.Reverse(plan.Done).ToList()) {
        try {
          if (plan.SameVolume) Directory.Move(PortCatalog.FsPath(folder.Value), PortCatalog.FsPath(folder.Key));
          else if (Directory.Exists(PortCatalog.FsPath(folder.Key))) Delete(folder.Value);
        } catch (Exception) { }
      }
      plan.Done.Clear();
    }

    // After the library was saved: removes the old copy when the port was copied to another drive.
    // Returns the folders that could not be removed so the user can delete them later.
    public static List<string> Finish(MovePlan plan) {
      var left = new List<string>();
      if (plan.SameVolume) return left;
      foreach (var folder in plan.Done) { try { Delete(folder.Key); } catch (Exception) { left.Add(folder.Key); } }
      return left;
    }

    static void Delete(string root) {
      foreach (string file in Directory.GetFiles(PortCatalog.FsPath(root), "*", SearchOption.AllDirectories)) File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
      Directory.Delete(PortCatalog.FsPath(root), true);
    }
  }
}
