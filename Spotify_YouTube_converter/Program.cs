using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Spotify_YouTube_converter
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //Getting playlist CSV data from exportify 
            Console.WriteLine("Please log in using your spotify account then export the desired public playlist ");
            Process.Start(new ProcessStartInfo("https://exportify.net") { UseShellExecute = true });

            Console.WriteLine("\nPlease enter the file path of the CSV file you just downloaded");
            string filePath = (Console.ReadLine()).Trim('"');

            List<Track> tracks = getTrackInfo(filePath);


            //Youtube api OAuth 
            List<string> vidIDs = new List<string>();
            Console.WriteLine("\nEnter your youtube API key");
            string apiKey = Console.ReadLine();
            Console.WriteLine("PLEASE WAIT");

            Console.Write("Enter your Google API Client ID: ");
            string clientID = Console.ReadLine();

            Console.Write("Enter your Google API Client Secret: ");
            string clientSecret = Console.ReadLine();

            string redirectUri = "http://127.0.0.1:5000/callback/";
            string authCode = GetAuthCode(clientID, redirectUri);
            var client = new HttpClient();

            var token = await exchangeAuthCode(authCode, clientID, clientSecret, redirectUri, client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

            Console.WriteLine("Finding videos... (please wait)");
            int trackNameCount = 0;
            foreach (Track item in tracks)
            {
                string search = item.Name + " " + item.Album + " " + item.Artist;
                Console.WriteLine("Searching for "+ search);
                vidIDs.Add((await GetYouTubeVidID(search, token.access_token, client)).items[0].id.videoId.ToString());
                Console.WriteLine(trackNameCount + " tracks fetched");
                trackNameCount++;
            }
            string playlistID;
            Console.WriteLine("\nWould you like you add to a new YoutTube playlist (any key) or use an existing one (1)?  ");
            Console.WriteLine("Using an exisitng playlist allows more songs to be added to the playlist (due to google api rules)");
            string playlistChoice =  Console.ReadLine();
            List<string> failedIDs = new List<string>();
            if (playlistChoice == "1")
            {
                Console.WriteLine("Enter the playlist ID (can be found in the URL of the playlist)");
                playlistID = Console.ReadLine();
                failedIDs = await InsertAllToPlaylist(token.access_token, vidIDs, playlistID, client);
                if (failedIDs.Count !=0)
                {
                    Console.WriteLine("Some videos failed to add\n");
                    foreach (string item in failedIDs)
                    {
                        Console.WriteLine(failedIDs);
                    }

                }
                else
                {
                    Console.WriteLine("All videos added successfully");
                }
            }
            else
            {
                playlistID = (await CreatePlaylist(token.access_token, client)).ToString();
                Console.WriteLine(playlistID);
                failedIDs = await InsertAllToPlaylist(token.access_token, vidIDs, playlistID, client);
                if (failedIDs.Count != 0)
                {
                    Console.WriteLine("Some videos failed to add\n");
                    foreach (string item in failedIDs)
                    {
                        Console.WriteLine("vid ID: "+ failedIDs);
                    }

                }
                else
                {
                    Console.WriteLine("All videos added successfully");
                }
            }
            Console.WriteLine("Done");
            Console.ReadKey();
        }
        static async Task<List<string>> InsertAllToPlaylist(string token, List<string> vidIDs, string playlistID, HttpClient client)
        {
            List<string> failedIDs = new List<string>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            int vidCount = 1;
            foreach (string item in vidIDs)
            {
                var body = new
                {
                    snippet = new
                    {
                        playlistId = playlistID,
                        resourceId = new
                        {
                            kind = "youtube#video",
                            videoId = item
                        }
                    }
                };
                Console.WriteLine("Insterting to "+ playlistID);
                string jsonBody = JsonSerializer.Serialize(body);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("https://www.googleapis.com/youtube/v3/playlistItems?part=snippet", content);
                var responseText = await response.Content.ReadAsStringAsync();
                Console.WriteLine();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Error: " + responseText);

                    failedIDs.Add(item);
                }
                else
                {
                    Console.WriteLine("Added " + vidCount++ + " videos to playlist");
                }
            }
            return failedIDs;
        }
        static async Task<string> CreatePlaylist(string token, HttpClient client)
        {
            Console.WriteLine("what would you like to name the playlist");
            string playlistName = Console.ReadLine();
            string playlistDescription = "created from api";

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                snippet = new { title = playlistName, description = playlistDescription },
                status = new { privacyStatus = "private" }
            };

            string jsonBody = JsonSerializer.Serialize(body);

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("https://www.googleapis.com/youtube/v3/playlists?part=snippet,status", content);
                var responseText = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseText);
            
            var playlistJson = JsonSerializer.Deserialize<YoutubePlaylistRoot>(responseText);


            Console.WriteLine("playlist created");
            return playlistJson.id;
        }
        static async Task<YoutubeRoot> GetYouTubeVidID(string search, string token, HttpClient client)
        {

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("https://www.googleapis.com/youtube/v3/search?part=snippet&q=" + search + "&type=video&maxResults=1");

            var responceText = await response.Content.ReadAsStringAsync();

            var vidID = JsonSerializer.Deserialize<YoutubeRoot>(responceText);


            Console.WriteLine();
            return vidID;
        }
        static string GetAuthCode(string clientID, string redirectUri)
        {
            string scope = "https://www.googleapis.com/auth/youtube";
            string loginUrl =
                "https://accounts.google.com/o/oauth2/v2/auth?" +
                "response_type=code" +
                "&client_id=" + Uri.EscapeDataString(clientID) +
                "&scope=" + Uri.EscapeDataString(scope) +
                "&redirect_uri=" + Uri.EscapeDataString(redirectUri);
            string authCode = null;
            HttpListenerContext context;
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(redirectUri);
                listener.Start();
                Console.WriteLine("\nOpening Spotify login in your browser...");
                Process.Start(new ProcessStartInfo(loginUrl) { UseShellExecute = true }); // 'UseShellExecute' means windows opens the process

                context = listener.GetContext();

                // Send a response so the browser doesn't hang
                string html = "<html><body><h2>Done! You can close this tab.</h2></body></html>";
                byte[] bytes = Encoding.UTF8.GetBytes(html);
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();

                authCode = context.Request.QueryString["code"];

                if (authCode == null)
                {
                    Console.WriteLine("Error: " + context.Request.QueryString["error"]);
                    Console.ReadKey();
                    Environment.Exit(1);
                }
                listener.Stop();
            }
            return authCode;
        }
        static async Task<TokenResponse> exchangeAuthCode(string authCode, string clientID, string clientSecret, string redirectUri, HttpClient client)
        {
            

            var parameters = new Dictionary<string, string>
            {
                { "client_id", clientID },
                { "client_secret", clientSecret },
                { "code", authCode },
                { "grant_type", "authorization_code" },
                { "redirect_uri", redirectUri }
            };

            var content = new FormUrlEncodedContent(parameters);

            var response = await client.PostAsync("https://oauth2.googleapis.com/token", content); 
            var responseText = await response.Content.ReadAsStringAsync();
            var token = JsonSerializer.Deserialize<TokenResponse>(responseText);

            if (token?.access_token == null) // ?. null conditional operator 
            {
                Console.WriteLine("Failed to get token. Response: " + responseText);
                Console.ReadKey();
                Environment.Exit(1);
            }
            Console.WriteLine();
            return token;
        }

        static List<Track> getTrackInfo(string filepath)
        {
            int counter = 0;
            string[] lines = File.ReadAllLines(filepath);
            string[] words;
            List <Track> tracks = new List<Track>();
            bool isFirst = true;
            foreach (string line in lines)
            {
                if (isFirst)
                {
                    isFirst = false;
                    continue;
                }
                words =line.Split(',');
                Track track = new Track();
                track.Name = words[1].Trim('"');
                track.Album = words[2].Trim('"');
                track.Artist = words[3].Trim('"');
                track.Release = words[4].Trim('"');
                tracks.Add(track);
                Console.WriteLine("Tracks processed: "+counter++);
            }
            return tracks;
        }

        public class YoutubePlaylistRoot
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public string id { get; set; }

        }
        public class YoutubeRoot
        {
            public List<Item> items { get; set; }
        }
        public class Item
        {
            public string kind { get; set; }
            public string etag { get; set; }
            public Id id { get; set; }

        }
        public class Id
        {
            public string kind { get; set; }
            public string videoId { get; set; }
        }
        public class TokenResponse
        {
            public string access_token { get; set; }
        }

        // code below no longer in use due to spotify policy changes 

        //static string GetSpotifyAccessToken()
        //{
        //    //no longer works due to spotify API policy changes you must have a premium acount now (helpful..)
        //    Console.Write("Enter your Spotify Client ID: ");
        //    string clientID = Console.ReadLine();

        //    Console.Write("Enter your Spotify Client Secret: ");
        //    string clientSecret = Console.ReadLine();

        //    string redirectUri = "http://127.0.0.1:5000/callback/";
        //    string scope = "user-read-private";

        //    string loginUrl =
        //        "https://accounts.spotify.com/authorize?" +
        //        "response_type=code" +
        //        "&client_id=" + Uri.EscapeDataString(clientID) +
        //        "&scope=" + Uri.EscapeDataString(scope) +
        //        "&redirect_uri=" + Uri.EscapeDataString(redirectUri);

        //    // Start listener BEFORE opening browser
        //    string authCode = null;
        //    using (var listener = new HttpListener())
        //    {
        //        listener.Prefixes.Add(redirectUri);
        //        listener.Start();
        //        Console.WriteLine("\nOpening Spotify login in your browser...");
        //        Process.Start(new ProcessStartInfo(loginUrl) { UseShellExecute = true }); // 'UseShellExecute' means windows opens the process




        //        Console.WriteLine("Waiting for you to log in...");

        //        // Blocks here until Spotify redirects back
        //        var context = listener.GetContext();

        //        // Send a response so the browser doesn't hang
        //        string html = "<html><body><h2>Finished You can close this tab.</h2></body></html>";
        //        byte[] bytes = Encoding.UTF8.GetBytes(html);
        //        context.Response.ContentLength64 = bytes.Length;
        //        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        //        context.Response.OutputStream.Close();

        //        authCode = context.Request.QueryString["code"];

        //        if (authCode == null)
        //        {
        //            Console.WriteLine("Error: " + context.Request.QueryString["error"]);
        //            Console.ReadKey();
        //            //Environment.Exit(1);
        //        }
        //    }

        //    Console.WriteLine("exchanging auth code for token");

        //    // Exchange the auth code for an access token
        //    var client = new HttpClient();
        //    string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientID + ":" + clientSecret));
        //    client.DefaultRequestHeaders.Add("Authorization", "Basic " + credentials);

        //var body = new StringContent(
        //    "grant_type=authorization_code&" +
        //    "code=" + Uri.EscapeDataString(authCode) +
        //    "&redirect_uri=" + Uri.EscapeDataString(redirectUri), Encoding.UTF8,
        //    "application/x-www-form-urlencoded"
        //);

        //    var response = client.PostAsync("https://accounts.spotify.com/api/token", body).Result;
        //    var responseText = response.Content.ReadAsStringAsync().Result;

        //    var token = JsonSerializer.Deserialize<TokenResponse>(responseText);

        //    if (token?.access_token == null) // ?. null conditional operator 
        //    {
        //        Console.WriteLine("Failed to get token. Response: " + responseText);
        //        Console.ReadKey();
        //        Environment.Exit(1);
        //    }

        //    return token.access_token;
        //}
        //static List<string> GetTracksFromPlaylist(string accessToken)
        //{

        //    Console.WriteLine("Enter your playlist ID");
        //    string playlistID = Console.ReadLine();
        //    string text;

        //    var client = new HttpClient();
        //    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

        //    var response = client.GetAsync("https://api.spotify.com/v1/playlists/" + playlistID + "/items").Result;
        //    var responseText = response.Content.ReadAsStringAsync().Result;

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        Console.WriteLine("Request failed: " + responseText);
        //        Console.ReadKey();
        //        Environment.Exit(1);
        //    }
        //    return null;

    }
}


    //public class TokenResponse
    //{
    //    public string access_token { get; set; }
    //}
    ////
    //public class Root
    //{
    //    public List<Item> items { get; set; }
    //}
    //public class Item
    //{
    //    public Track track { get; set; }

    //}
    //public class Artists
    //{
    //    public string name { get; set; }
    //}
    //public class Track
    //{
    //    public string name { get; set; }
    //    public List<Artists> artists { get; set; }
    //}
    ////
    //public class SpotifyTrack
    //{
    //    public string name { get; set; }
    //}

    //public class acesstoken
    //{
    //    public string access_token { get; set; }
    //}

    


