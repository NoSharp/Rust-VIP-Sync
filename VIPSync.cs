using Oxide.Core;
using Oxide.Core.Database;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("VIPSync", "NoSharp", "1.0.0")]
    class VIPSync : RustPlugin
    {

        #region Fields

        // For when I'm debugging this plugin.
        bool Debug = true;

        // Instantiates the Umod/oxide SqlLibrary.
        private Core.MySql.Libraries.MySql sqlLibrary = Interface.Oxide.GetLibrary<Core.MySql.Libraries.MySql>();

        // A named variable for Sql Connection.
        private Connection sqlConnection;

        #endregion Fields

        /**
         * Table Plan
         * TABLE: CONFIG[TABLE]
         * VALUES
         * UserID Player ID (string)
         * Ranks CSV roles.
         */

        #region Oxide Hooks

        /// <summary>
        /// Establishes connection with the database.
        /// </summary>
        void Init()
        {
            // Establish connection to db with configured values.
            

            // Run table check if the table exists.
            TableCheck();


            foreach (var player in BasePlayer.activePlayerList)
            {
                SelectPlayer(player);
            }
        }

        void OnPlayerInit(BasePlayer player)
        {
            SelectPlayer(player);
        }

        /// <summary>
        /// When a player is granted a group.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="permName"></param>
        void OnUserGroupAdded(string id, string groupName)
        {
            if (Debug) Puts($"Groups Added {id} ({groupName}) ");
            
            IPlayer player = covalence.Players.FindPlayerById(id);
            

            UpdatePlayer(player);


        }

        /// <summary>
        /// When a player is removed from a group.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="permName"></param>
        void OnUserGroupRemoved(string id, string groupName)
        {
            if (Debug) Puts($"Groups Removed {id} ({groupName}) ");

            IPlayer player = covalence.Players.FindPlayerById(id);
            UpdatePlayer(player);
        }

        /// <summary>
        /// Used when creating a default configuration file.
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            
            Puts("CREATING NEW CONFIGURATION FILE");
            Config["Table"] = "RanksTable";
            Config["Ip"] = "127.0.0.1";
            Config["Database"] = "MainDB";
            Config["Username"] = "root";
            Config["Password"] = "root";
            Config["Port"] = "3306";

        }

        #endregion Oxide Hooks

        #region MySql Utils

        /// <summary>
        /// Checks if a table exists, if not, let's create one.
        /// </summary>
        void TableCheck()
        {
            

            sqlConnection = sqlLibrary.OpenDb(
                Config["Ip"] as string,
                int.Parse(Config["Port"] as string),
                Config["Database"] as string,
                Config["Username"] as string,
                Config["Password"] as string,
                this);
            // SQL syntax for table creation.
            string tableQuery = $"CREATE TABLE IF NOT EXISTS {Config["Table"] as string}(UserID varchar(255), Ranks TEXT)";

            // Create SQL command.
            Sql command = Sql.Builder.Append(tableQuery);

            // Don't need to do anything with the call back, and query the database.
            sqlLibrary.Query(command, sqlConnection, callback => { });
        }

        /// <summary>
        /// Get's the players' roles when they join.
        /// </summary>
        /// <param name="player"></param>
        /// <returns>A list of player Roles</returns>
        void SelectPlayer(BasePlayer player)
        {
            string userId = player.UserIDString;
            string selectQuery =  $"SELECT * FROM {Config["Table"] as string} WHERE UserID = {userId} ";
            if (Debug) Puts(selectQuery);
            Sql command = Sql.Builder.Append(selectQuery);
            string rolesUnParsed = "";
            List<string> ParsedRoles = new List<string>();
            sqlLibrary.Query(command, sqlConnection, returned =>
            {
                if (returned.Count == 0)
                {
                    if (Debug) Puts("Returned is Null");

                    InsertPlayer(player);
                    
                    return;
                }

                if (Debug) Puts(Newtonsoft.Json.JsonConvert.SerializeObject(returned, Newtonsoft.Json.Formatting.Indented));

                foreach (Dictionary<string, object> entry in returned)
                {
                    if (entry["Ranks"] as string == null)
                    {
                        Puts("RANK IS NULL");
                        UpdatePlayer(player.IPlayer);
                        return;
                    }

                    if (Debug) Puts(entry["UserID"] as string  + "|||" + entry["Ranks"] as string);

                    

                    rolesUnParsed = entry["Ranks"] as string;
                    ParsedRoles = ParseRoles(rolesUnParsed);
                    GroupCheck(ParsedRoles, player);
                    return;
                }
            });


            return;
        }

        /// <summary>
        /// A function to insert a player into the Mysql Database.
        /// </summary>
        /// <param name="player"></param>
        void InsertPlayer(BasePlayer player)
        {

            string ranks = string.Join(",", permission.GetUserGroups(player.IPlayer.Id));

            string query = $"INSERT INTO {Config["Table"] as string} (UserID, Ranks) VALUES ({player.UserIDString}, \"{ranks}\")";

            if (Debug) Puts(query);

            Sql command = Sql.Builder.Append(query);

            sqlLibrary.Insert(command, sqlConnection);

        }

        /// <summary>
        /// Used to update the player on the Mysql Database.
        /// </summary>
        /// <param name="ply"></param>
        void UpdatePlayer(IPlayer ply)
        {
            string ranks = string.Join(",", permission.GetUserGroups(ply.Id));
            string userId = BasePlayer.activePlayerList.Find(x => x.UserIDString == ply.Id).UserIDString;
            string query = $"UPDATE {Config["Table"] as string} SET Ranks = \"{ranks}\" WHERE UserID = {userId}";

            if (Debug) Puts(query);

            Sql command = Sql.Builder.Append(query);

            sqlLibrary.Insert(command, sqlConnection);

        }

        #endregion MySql Utils

        #region Utils

        /// <summary>
        /// Used to add groups to player.
        /// </summary>
        /// <param name="roles"></param>
        /// <param name="player"></param>
        void GroupCheck(List<string> roles, BasePlayer player)
        {
            UserData data = permission.GetUserData(player.IPlayer.Id);

            foreach (var role in permission.GetUserGroups(player.IPlayer.Id))
            {
                if (!roles.Contains(role))
                {
                    data.Groups.Remove(role);

                }

            }


            foreach (string role in roles)
            {
               

                if (!permission.UserHasGroup(player.IPlayer.Id, role))
                {

                    data.Groups.Add(role);
                }

            }

        }

        /// <summary>
        /// Converts Comma Seperated Roles into a List<string>.
        /// </summary>
        /// <param name="roles"></param>
        /// <returns> A list of roles from the Comma Seperated Roles</returns>
        List<string> ParseRoles(string roles)
        {

            var ParsedRoles = new List<string>();
            foreach (var role in roles.Split(','))
            {
                ParsedRoles.Add(role);
            }

            return ParsedRoles;
        }

        #endregion Utils

    }
}
