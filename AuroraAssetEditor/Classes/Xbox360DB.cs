using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Diagnostics;
using AuroraAssetEditor.Models;
using System.Collections.Generic;


namespace AuroraAssetEditor.Classes
{
    public class GameDataResult
    {
        public bool Success { get; set; }
        public Xbox360GameData Data { get; set; }
        public string Error { get; set; }
    }

    public static class Xbox360DB
    {
        private static SQLiteConnection _connection;
        private const string DatabasePath = @"./databases/xbox360.db";
        private static Dictionary<string, Xbox360GameData> _gameDataCache = new Dictionary<string, Xbox360GameData>();

        public static void Initialize()
        {
            if (_connection == null)
            {
                var connectionString = $"Data Source={DatabasePath};Version=3;";
                _connection = new SQLiteConnection(connectionString);
            }
            
            if (!File.Exists(DatabasePath))
                throw new FileNotFoundException("Xbox360 database not found", DatabasePath);
        }

        public static void LoadAllGameData()
        {
            try
            {
                _connection.Open();
                
                const string query = @"SELECT 
                    TitleID, 
                    GameName,
                    Developer,
                    Publisher,
                    Description,
                    ReleaseDate,
                    related_entries as Variants
                    FROM vw_xbox360_unified";

                SQLiteCommand cmd = new SQLiteCommand(query, _connection);
                // Debug.WriteLine("Loading all game data from database");

                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var gameData = new Xbox360GameData
                    {
                        TitleID = reader["TitleID"].ToString(),
                        GameName = reader["GameName"]?.ToString() ?? "",
                        Developer = reader["Developer"]?.ToString() ?? "",
                        Publisher = reader["Publisher"]?.ToString() ?? "",
                        Description = reader["Description"]?.ToString() ?? "",
                        ReleaseDate = reader["ReleaseDate"]?.ToString() ?? "",
                        Variants = reader["Variants"]?.ToString() ?? ""
                    };
                    _gameDataCache[gameData.TitleID] = gameData;
                }

                reader.Dispose();
                cmd.Dispose();
                // Debug.WriteLine($"Loaded {_gameDataCache.Count} game entries into cache");
            }
            catch (Exception ex)
            {
                MainWindow.SaveError(ex);
                // Debug.WriteLine($"Error loading game data: {ex.Message}");
            }
            finally
            {
                _connection.Close();
            }
        }

        public static GameDataResult GetGameDataByTitleID(string titleId)
        {
            if (_gameDataCache.TryGetValue(titleId, out var gameData))
            {
                return new GameDataResult { Success = true, Data = gameData };
            }
            return new GameDataResult { Success = false, Error = "No game found with specified TitleID" };
        }
    }
} 