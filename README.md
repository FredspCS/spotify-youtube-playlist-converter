# Spotify Playlist To YouTube Playlist converter

Console app that converts a Spotify playlist into a YouTube playlist by matching each track to a YouTube video and then adding it to a new or existing playlist.

## How it works 
1. Firstly the desired playlist will be converted into a CSV file using [Exportify](https://exportify.net)
2. The app then reads the CSV file and searches YouTube for each track (using the given fields from the CSV file such as track name, album, and artist)
3. The best match to the search is then added to the new or existing playlist 

## Usage
1. Clone the repo and open `Spotify_YouTube_converter.sln` in Visual Studio
2. Build and run
3. You'll be prompted for:
   - Your exported Spotify CSV file path
   - A YouTube Data API key
   - Your Spotify Client ID and Secret
4. Log in via the browser prompts when asked
5. Choose to create a new playlist or add to an existing one
