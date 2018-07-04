namespace ethermine
{
    class MySQLInfo
    {
        public const string Server = "127.0.0.1";
        public const string Database = "ethermine";
        public const string Port = "3306";
        public const string DB_UID = "root";
        public const string DB_Pwd = "a7778101";
        public const string Timeout = "5";

        /// <summary>
        /// 取得連線字串
        /// </summary>
        /// <returns>回傳連線字串</returns>
        public static string GetConnectionString()
        {
            return string.Format("server={0};database={1};uid={3};pwd={4}; Port={2}; Connect Timeout={5}; Charset=utf8", Server, Database, Port, DB_UID, DB_Pwd, Timeout);
        }

    }
}
