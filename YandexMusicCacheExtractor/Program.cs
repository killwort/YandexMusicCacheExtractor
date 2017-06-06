using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YandexMusicCacheExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            var packagesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            var ymPaths = Directory.GetDirectories(packagesPath, "*Yandex.Music*");
            if(!ymPaths.Any())
            {
                Console.Error.WriteLine("Cannot find installed Yandex.Music");
                return;
            }
            var oo = args.Length > 0 ? File.CreateText(args[0]) : Console.Out;
            var musicDbPath = Directory.GetFiles(Path.Combine(ymPaths[0], "LocalState"), "musicdb_*.sqlite");
            foreach(var mdb in musicDbPath)
            {
                var mdb_ = Path.GetFileNameWithoutExtension(mdb).Substring(8);
                Console.Error.WriteLine("Working on DB {0}", mdb_);
                var files=Directory.GetFiles(Path.Combine(ymPaths[0], "LocalState", "Music", mdb_), "*.mp3")
                    .Concat(Directory.GetFiles(Path.Combine(ymPaths[0], "LocalState", "MusicCache", mdb_), "*.cached"));
                var ids = files.Select(x =>
                {
                    int rv;
                    if (int.TryParse(Path.GetFileNameWithoutExtension(x), out rv)) return rv;
                    return 0;
                }).Where(x => x != 0).Distinct().ToArray();
                using (var cnn = new System.Data.SQLite.SQLiteConnection($"Data Source={mdb}"))
                {                    
                    cnn.Open();
                    using(var cmd=cnn.CreateCommand())
                    {
                        cmd.CommandText = $"select T_Track.Id, T_Track.Title, a.Name, l.Title, Year, GenreId from T_Track left join T_TrackAlbum ll on ll.TrackId=T_Track.Id left join T_TrackArtist aa on aa.TrackId = T_Track.Id left join T_Album l on l.Id = ll.AlbumId left join T_Artist a on a.Id = aa.ArtistId where T_Track.Id IN ({string.Join(",",ids)})";
                        var readIds = new HashSet<string>();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var id = reader.GetString(0);
                                if (readIds.Contains(id)) continue;
                                readIds.Add(id);
                                var title = reader.GetString(1);
                                var artist = reader.GetString(2);
                                var album = reader.GetString(3);
                                var year = reader.GetString(4);
                                var genre = reader.GetString(5);
                                string src;
                                if (File.Exists(Path.Combine(ymPaths[0], "LocalState", "Music", mdb_, id.ToString() + ".mp3")))
                                    src = Path.Combine(ymPaths[0], "LocalState", "Music", mdb_, id.ToString() + ".mp3");
                                else if (File.Exists(Path.Combine(ymPaths[0], "LocalState", "MusicCache", mdb_, id.ToString() + ".cached")))
                                    src = Path.Combine(ymPaths[0], "LocalState", "MusicCache", mdb_, id.ToString() + ".cached");
                                else
                                    continue;
                                string target = $"{artist} - {album} - {title}.mp3";
                                var inv = Path.GetInvalidFileNameChars();
                                target = new string(target.Select(x => (inv.Contains(x)||x<32) ? '_' : x).ToArray());
                                oo.WriteLine($"ffmpeg -i \"{src}\" -metadata title=\"{title}\" -metadata artist=\"{artist}\" -metadata album=\"{album}\" -b:a 256k \"{target}\"");
                            }
                        }
                    }
                    oo.Close();
                    //var sh=cnn.GetSchema();
                }
            }
                //@"C:\Documents and Settings\Andrey\AppData\Local\Packages\A025C540.Yandex.Music_vfvw9svesycw6";
        }
    }
}
