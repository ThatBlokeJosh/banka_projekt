using System.Data.SQLite;

namespace Banka
{
    public class User
    {
        public int uid;
        public string username;
        public string password;
        public string role;

        public User(string username, string password, string role, int uid)
        {
            this.username = username;
            this.password = password;
            this.role = role;
            this.uid = uid;
        }
    }

    public class Account
    {
        public int account_id;
        public int uid;
        public string name;
        public string status;
        public string type;
        public double balance;

        public Account(int account_id, int uid, string name, string status, string type, double balance)
        {
            this.account_id = account_id;
            this.uid = uid;
            this.name = name;
            this.status = status;
            this.type = type;
            this.balance = balance;
        }
    }

    public class Transaction
    {
        public int transaction_id;
        public int from_id;
        public int to_id;
        public double amount;
        public string type;
        public DateTime timestamp;

        public Transaction(int transaction_id, int from_id, int to_id, double amount, string type, DateTime timestamp)
        {
            this.transaction_id = transaction_id;
            this.from_id = from_id;
            this.to_id = to_id;
            this.amount = amount;
            this.type = type;
            this.timestamp = timestamp;
        }
    }

    public class Log
    {
        public int log_id;
        public string title;
        public string severity;
        public DateTime timestamp;

        public Log(int log_id, string title, string severity, DateTime timestamp)
        {
            this.log_id = log_id;
            this.title = title;
            this.severity = severity;
            this.timestamp = timestamp;
        }
    }
}