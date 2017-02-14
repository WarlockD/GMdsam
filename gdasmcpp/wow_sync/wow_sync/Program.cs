using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;

namespace wow_sync
{
    class Program
    {
        const string auth_jsion = @"{""installed"":{""client_id"":""812813560883-rjgtigdh6fli27mljvapl6v58h93t2j1.apps.googleusercontent.com"",""project_id"":""aep-simplesite"",""auth_uri"":""https://accounts.google.com/o/oauth2/auth"",""token_uri"":""https://accounts.google.com/o/oauth2/token"",""auth_provider_x509_cert_url"":""https://www.googleapis.com/oauth2/v1/certs"",""client_secret"":""plyqqJGWk-G2B-gKkyEAq9P4"",""redirect_uris"":[""urn:ietf:wg:oauth:2.0:oob"",""http://localhost""]}}";
        const string auth_name = "WowSyncAlpha";
        const string auth_id = "812813560883-rjgtigdh6fli27mljvapl6v58h93t2j1.apps.googleusercontent.com";

        // If modifying these scopes, delete your previously saved credentials
        // at ~/.credentials/drive-dotnet-quickstart.json
        static string[] Scopes = { DriveService.Scope.DriveAppdata };
        static string ApplicationName = "Drive API .NET Quickstart";

       static  DriveService login()
        {
            UserCredential credential;

            using (var stream = new MemoryStream(Encoding.ASCII.GetBytes(auth_jsion)))
            {
                string credPath = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials/drive-dotnet-quickstart.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }
            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,

                //    fileMetadata.Name = "config.json";
                // fileMetadata.Parents = new List<string>() { "appDataFolder" };
            });
            return service;
        }
        /*
   
        static List<DirectoryInfo> GetSubDirs(IEnumerable<DirectoryInfo> dirs)
        {
            List<DirectoryInfo> ret = new List<DirectoryInfo>(dirs);
            foreach (var dir in dirs)  ret.AddRange(GetSubDirs(dir.EnumerateDirectories()));
            return ret;
        }
        static List<DirectoryInfo> getdirectorys(string dirPath)
        {
            try
            {
                DirectoryInfo root = new DirectoryInfo(dirPath); root.EnumerateDirectories();
                List<DirectoryInfo> dirs = GetSubDirs(root.EnumerateDirectories());
                root.
              
                Console.WriteLine("{0} directories found.", dirs.Count);
                return dirs;
            }
            catch (UnauthorizedAccessException UAEx)
            {
                Console.WriteLine(UAEx.Message);
            }
            catch (PathTooLongException PathEx)
            {
                Console.WriteLine(PathEx.Message);
            }
            return null;
        }
        */
    
        static void Main(string[] args)
        {
            string folder = args[0];
            string zip_file_name = Path.GetTempFileName();
            string test_file = "test.zip";
            //   getdirectorys(args[0]);
        //    tar_cs.TarMaker maker = new tar_cs.TarMaker(folder);
            tar_cs.TarFucker test = new tar_cs.TarFucker(folder);
            test.Close();
            var info = new FileInfo(test_file);
            if (info != null) info.Delete();
            ZipFile.CreateFromDirectory(folder, test_file, CompressionLevel.Optimal,true);// zip_file_name);
         
            var service = login();

            var request = service.Files.List();
            request.Spaces = "appDataFolder";
            request.Fields = "nextPageToken, files(id, name)";
            request.PageSize = 10;
            var result = request.Execute();
            if (result.Files != null && result.Files.Count > 0)
            {
                foreach (var file in result.Files)
                {
                    Console.WriteLine(String.Format(
                        "Found file: %s (%s)", file.Name, file.Id));
                }
            } else
            {
                Console.WriteLine("No files found.");
            }

            Console.Read();

        }
    }
}

