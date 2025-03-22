using System.Data.SQLite;

namespace Banka
{

    public abstract class Table<T>
    {
        public string name;
        public SQLiteCommand cmd;

        public Table(string name, SQLiteCommand cmd)
        {
            this.name = name;
            this.cmd = cmd;
        }

        public abstract (string cols, string values) ParseInsert(T item);

        public virtual int Insert(T item)
        {
            (string cols, string values) = ParseInsert(item);
            cmd.CommandText = $"INSERT INTO {name}({cols}) VALUES({values});";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "SELECT last_insert_rowid()";
            int rowId = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = $"SELECT * FROM {name} WHERE rowid={rowId}";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }


        public abstract T ParseReader(SQLiteDataReader reader);
        public virtual List<T> SelectAll(string condition="")
        {
            cmd.CommandText = $"SELECT * FROM {name} {condition}";
            List<T> users = new List<T>();
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    users.Add(ParseReader(reader));
                }
            }
            return users;
        }
    }

    public class Users : Table<User>
    {
        public string name;
        public SQLiteCommand cmd;
        public Users(string name, SQLiteCommand cmd) : base(name, cmd)
        {
            this.name = name;
            this.cmd = cmd;
        }

        public override (string cols, string values) ParseInsert(User item)
        {
            return (cols: "username, password, role", values: $"\"{item.username}\", \"{item.password}\", \"{item.role}\"");
        }

        public override User ParseReader(SQLiteDataReader reader)
        {
            return new User((string)reader["username"], (string)reader["password"], (string)reader["role"], reader.GetInt32(reader.GetOrdinal("uid")));
        }

        public User Select(string username)
        {
            cmd.CommandText = $"SELECT * FROM {name} WHERE username='{username}'";
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    return ParseReader(reader);
                }
            }
            return null;
        }
    }

    public class Transactions : Table<Transaction>
    {
        public string name;
        public SQLiteCommand cmd;
        public Transactions(string name, SQLiteCommand cmd) : base(name, cmd)
        {
            this.name = name;
            this.cmd = cmd;
        }

        public override (string cols, string values) ParseInsert(Transaction item)
        {
            return (cols: "from_id, to_id, amount, type, timestamp", values: $"{item.from_id}, {item.to_id}, {Math.Round(item.amount, 5)}, \"{item.type}\", \"{item.timestamp}\"");
        }

        public override Transaction ParseReader(SQLiteDataReader reader)
        {
            return new Transaction(
                reader.GetInt32(reader.GetOrdinal("transaction_id")), reader.GetInt32(reader.GetOrdinal("from_id")), 
                reader.GetInt32(reader.GetOrdinal("to_id")), reader.GetDouble(reader.GetOrdinal("amount")), 
                (string)reader["type"], DateTime.Parse((string)reader["timestamp"]));
        }

        public Transaction Select(int id)
        {
            cmd.CommandText = $"SELECT * FROM {name} WHERE transaction_id={id}";
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    return ParseReader(reader);
                }
            }
            return null;
        }
    }

    public class Logs : Table<Log>
    {
        public string name;
        public SQLiteCommand cmd;
        public Logs(string name, SQLiteCommand cmd) : base(name, cmd)
        {
            this.name = name;
            this.cmd = cmd;
        }

        public override (string cols, string values) ParseInsert(Log item)
        {
            return (cols: "title, severity, timestamp", values: $"\"{item.title}\", \"{item.severity}\", \"{item.timestamp}\"");
        }

        public override Log ParseReader(SQLiteDataReader reader)
        {
            return new Log(
                reader.GetInt32(reader.GetOrdinal("log_id")), (string)reader["title"], (string)reader["severity"], DateTime.Parse((string)reader["timestamp"]));
        }
    }

    public class Accounts : Table<Account>
    {
        public string name;
        public SQLiteCommand cmd;
        public Accounts(string name, SQLiteCommand cmd) : base(name, cmd)
        {
            this.name = name;
            this.cmd = cmd;
        }

        public override (string cols, string values) ParseInsert(Account item)
        {
            return (cols: "name, uid, type", values: $"\"{item.name}\", {item.uid}, \"{item.type}\"");
        }

        public override Account ParseReader(SQLiteDataReader reader)
        {
            return new Account(reader.GetInt32(reader.GetOrdinal("account_id")), reader.GetInt32(reader.GetOrdinal("uid")), (string)reader["name"], "Good", (string)reader["type"], 0);
        }

        public Account Select(int account_id)
        {
            cmd.CommandText = $"SELECT * FROM {name} WHERE account_id={account_id}";
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    return ParseReader(reader);
                }
            }
            return null;
        }

        public void Delete(int id)
        {
            cmd.CommandText = $"DELETE FROM {name} WHERE account_id={id}";
            cmd.ExecuteNonQuery();
        }
    }

    public class DB
    {
        private const string connString = "Data Source=Db.sqlite;Version=3";
        private const string userTableSchema = @"CREATE TABLE IF NOT EXISTS [Users] (
            [uid] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            [username] TEXT NOT NULL UNIQUE,
            [password] TEXT NOT NULL,
            [role] TEXT NOT NULL
        )";
        private const string accountTableSchema = @"CREATE TABLE IF NOT EXISTS [Accounts] (
            [account_id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            [uid] INTEGER NOT NULL,
            [name] TEXT NOT NULL,
            [type] TEXT NOT NULL,
            FOREIGN KEY (uid) REFERENCES Users(uid)
        )";
        private const string transactionTableSchema = @"CREATE TABLE IF NOT EXISTS [Transactions] (
            [transaction_id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            [from_id] INTEGER,
            [to_id] INTEGER,
            [amount] REAL NOT NULL,
            [type] TEXT NOT NULL,
            [timestamp] TEXT NOT NULL,
            FOREIGN KEY (from_id) REFERENCES Account(account_id)
            FOREIGN KEY (to_id) REFERENCES Accounts(account_id)
        )";

        private const string logTableSchema = @"CREATE TABLE IF NOT EXISTS [Logs] (
            [log_id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            [title] TEXT NOT NULL,
            [severity] TEXT NOT NULL,
            [timestamp] TEXT NOT NULL
        )";

        private static SQLiteCommand cmd;

        public Users users;
        public Accounts accounts;
        public Transactions transactions;
        public Logs logs;

        public DB()
        {
            try
            {
                SQLiteConnection conn = new SQLiteConnection(connString);
                conn.Open();
                cmd = new SQLiteCommand(conn);
                cmd.CommandText = userTableSchema;
                cmd.ExecuteNonQuery();
                cmd.CommandText = accountTableSchema;
                cmd.ExecuteNonQuery();
                cmd.CommandText = transactionTableSchema;
                cmd.ExecuteNonQuery();
                cmd.CommandText = logTableSchema;
                cmd.ExecuteNonQuery();

                users = new Users("Users", cmd);
                accounts = new Accounts("Accounts", cmd);
                transactions = new Transactions("Transactions", cmd);
                logs = new Logs("logs", cmd);
            } catch (Exception e)
            {
                Console.WriteLine($"{e.Source}: {e.Message}");
                throw;
            }

        }
    }
}