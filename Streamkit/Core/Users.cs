﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

using Streamkit.Database;
using Streamkit.Crypto;
using Streamkit.Twitch;

namespace Streamkit.Core {
    public class User {
        private string userId;
        private string twitchId;
        private string twitchUsername;
        private string twitchToken;

        public User(string userId, string twitchId, string twitchUsername, string twitchToken) {
            this.userId = userId;
            this.twitchId = twitchId;
            this.twitchUsername = twitchUsername;
            this.twitchToken = twitchToken;
        }

        public string UserId {
            get { return this.userId; }
        }

        public string TwitchId {
            get { return this.twitchId; }
        }

        public string TwitchUsername {
            get { return this.twitchUsername; }
            set { this.twitchUsername = value; }
        }

        public string TwitchToken {
            get { return this.twitchToken; }
            set { this.twitchToken = value; }
        }
    }

    
    public static class UserManager {
        public static User GetUser(string userId) {
            using (DatabaseConnection conn = new DatabaseConnection()) {
                MySqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT user_id, twitch_id, twitch_username, twitch_token "
                                + "FROM view_users WHERE user_id = @id";
                cmd.Parameters.AddWithValue("@id", userId);

                MySqlDataReader reader = cmd.ExecuteReader();
                if (!reader.HasRows) {
                    throw new Exception("User " + userId + " does not exist.");
                }

                reader.Read();
                return new User(
                        reader.GetString("user_id"), 
                        reader.GetString("twitch_id"), 
                        reader.GetString("twitch_username"), 
                        AES.Decrypt(reader.GetString("twitch_token")));
            }
        }

        public static User GetUserTwitch(string username) {
            using (DatabaseConnection conn = new DatabaseConnection()) {
                MySqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT user_id, twitch_id, twitch_username, twitch_token "
                                + "FROM view_users WHERE twitch_username = @username";
                cmd.Parameters.AddWithValue("@username", username);

                MySqlDataReader reader = cmd.ExecuteReader();
                if (!reader.HasRows) {
                    throw new Exception("User " + username + " does not exist.");
                }

                reader.Read();

                // Try catch so that we can still continue on testing with dummy tokens.
                string token = null;
                try {
                    token = AES.Decrypt(reader.GetString("twitch_token"));
                }
                catch { }

                return new User(
                        reader.GetString("user_id"),
                        reader.GetString("twitch_id"),
                        reader.GetString("twitch_username"),
                        token);
            }
        }

        public static List<string> GetTwitchUsernames() {
            using (DatabaseConnection conn = new DatabaseConnection()) {
                MySqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT twitch_username FROM users_twitch";
                MySqlDataReader reader = cmd.ExecuteReader();

                List<string> usernames = new List<string>();
                while (reader.Read()) {
                    usernames.Add(reader.GetString("twitch_username"));
                }
                return usernames;
            }
        }

        public static bool UserExists(string userId) {
            using (DatabaseConnection conn = new DatabaseConnection()) {
                MySqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT user_id FROM view_users WHERE twitch_id = @id";
                cmd.Parameters.AddWithValue("@id", userId);
                MySqlDataReader reader = cmd.ExecuteReader();
                return reader.HasRows;
            }
        }

        public static bool TwitchUserExists(string twitchId) {
            using (DatabaseConnection conn = new DatabaseConnection()) {
                MySqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT twitch_id FROM view_users WHERE twitch_id = @id";
                cmd.Parameters.AddWithValue("@id", twitchId);
                MySqlDataReader reader = cmd.ExecuteReader();
                return reader.HasRows;
            }
        }

        public static void InsertUser(User user) {
            using (DatabaseConnection conn = new DatabaseConnection()) {
                conn.BeginTransaction();

                try {
                    MySqlCommand cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO users (user_id, twitch_id) "
                                    + "VALUES (@userid, @twitchid)";
                    cmd.Parameters.AddWithValue("@userid", user.UserId);
                    cmd.Parameters.AddWithValue("@twitchid", user.TwitchId);
                    cmd.ExecuteNonQuery();

                    cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO users_twitch (twitch_id, user_id, twitch_username, twitch_token) "
                                    + "VALUES (@twitchid, @userid, @username, @token)";
                    cmd.Parameters.AddWithValue("@twitchid", user.TwitchId);
                    cmd.Parameters.AddWithValue("@userid", user.UserId);
                    cmd.Parameters.AddWithValue("@username", user.TwitchUsername);
                    cmd.Parameters.AddWithValue("@token", AES.Encrypt(user.TwitchToken));
                    cmd.ExecuteNonQuery();

                    conn.Commit();
                }
                catch {
                    conn.Rollback();
                    throw;
                }
            }

            TwitchBot.Instance.JoinChannel(user.TwitchUsername);
        }

        public static void UpdateUser(User user) {
            using (DatabaseConnection conn = new DatabaseConnection()) {
                MySqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE users_twitch "
                                + "SET twitch_username = @username, twitch_token = @token "
                                + "WHERE user_id = @id";
                cmd.Parameters.AddWithValue("@username", user.TwitchUsername);
                cmd.Parameters.AddWithValue("@token", AES.Encrypt(user.TwitchToken));
                cmd.Parameters.AddWithValue("@id", user.UserId);
                cmd.ExecuteNonQuery();
            }
        }

        public static User UpsertUser(string twitchId, string twitchUsername, string twitchToken) {
            if (!TwitchUserExists(twitchId)) {
                // Run insert if user does not exist.
                string userId = TokenGenerator.Generate();

                // Make sure the user id we create is unique.
                while (UserExists(userId)) {
                    userId = TokenGenerator.Generate();
                }

                User user = new User(userId, twitchId, twitchUsername, twitchToken);
                InsertUser(user);
                return user;
            }
            else {
                // Otherwise we update the twitch username and token.
                User user = GetUserTwitch(twitchUsername);
                user.TwitchUsername = twitchUsername;
                user.TwitchToken = twitchToken;

                UpdateUser(user);
                return user;
            }
           
        }
    }
}
