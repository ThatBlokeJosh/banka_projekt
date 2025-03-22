using ConsoleTables;
using System.Security.Principal;

namespace Banka
{
    public class Interface
    {
        DB db;
        User user;
        DateTime date;
        const double interestRate = 0.3;
        const double creditRate = 0.4;
        const int interestFreePeriod = 30;

        public Interface(DB db)
        {
            this.db = db;
            date = DateTime.Now;
        }

        string Readline(string message)
        {
            Console.Write(message + " ");
            return Console.ReadLine();
        }

        string ReadOptions(string message, string[] options)
        {
            while (true)
            {
                Console.Write(message + " ");
                string input = Console.ReadLine();
                if (options.Contains(input))
                {
                    return input;
                }
                Console.WriteLine("Invalid option");
            }
        }

        int ReadInt(string message)
        {
            while (true)
            {
                Console.Write(message + " ");
                string input = Console.ReadLine();
                if (int.TryParse(input, out int value))
                {
                    return value;
                }
                Console.WriteLine("Invalid option, try again");
            }
        }

        double ReadDouble(string message)
        {
            while (true)
            {
                Console.Write(message + " ");
                string input = Console.ReadLine();
                if (double.TryParse(input, out double value))
                {
                    return value;
                }
                Console.WriteLine("Invalid option, try again");
            }
        }

        Account ReadAccount(string message, bool enforce_ownership=true)
        {
            while (true)
            {
                int id = ReadInt(message);
                Account account = db.accounts.Select(id);

                if (account == null)
                {
                    Console.WriteLine("Account doesn't exist");
                    continue;
                }
                else if ((enforce_ownership && account.uid != user.uid) && user.role != "banker")
                {
                    Console.WriteLine("You do not own this account");
                    continue;
                }

                return account;
            }
            return null;
        }

        void CalculateInterest(Account account)
        {
            if (account.type != "Savings")
            {
                return;
            }

            List<Transaction> outgoing = db.transactions.SelectAll($"WHERE from_id={account.account_id}");
            List<Transaction> incoming = db.transactions.SelectAll($"WHERE to_id={account.account_id}");
            Dictionary<string, double> balance = new Dictionary<string, double>();
            Dictionary<string, bool> paidInterest = new Dictionary<string, bool>();

            foreach (Transaction income in incoming)
            {
                string day = income.timestamp.Date.ToString();

                if (income.type == "interest" && !paidInterest.ContainsKey(day))
                {
                    string key = DateTime.Parse(day).AddDays(-1).ToString();
                    paidInterest.Add(key, true);
                }

                if (balance.ContainsKey(day))
                {
                    balance[day] += income.amount;
                } else
                {
                    balance.Add(day, income.amount);
                }
            }
            foreach (Transaction outcome in outgoing)
            {
                string day = outcome.timestamp.Date.ToString();
                if (balance.ContainsKey(day))
                {
                    balance[day] -= outcome.amount;
                }
                else
                {
                    balance.Add(day, -1*outcome.amount);
                }
            }

            double prevBalance = 0;

            KeyValuePair<string, double> firstBalance = balance.First();
            DateTime first = DateTime.Parse(firstBalance.Key);
            int difference = date.Date.Subtract(first).Days;

            for (int i = 0; i <= difference; i++)
            {
                DateTime day = first.AddDays(i);
                string key = day.Date.ToString();
                double account_change = balance.ContainsKey(key) ? balance[key] : 0;
                prevBalance += account_change;
                double interest = prevBalance * interestRate * 0.0028;
                prevBalance += interest;
                if (day < date.Date && !paidInterest.ContainsKey(key))
                {
                    DateTime timestamp = day.AddDays(1);
                    db.transactions.Insert(new Transaction(0, 0, account.account_id, interest, "interest", timestamp));
                    db.logs.Insert(new Log(0, $"Interest payment to {account.account_id}", "INFO", DateTime.Now));
                }
            }
        }

        void CalculateCredit(Account account)
        {
            if (account.type != "Credit")
            {
                return;
            }

            List<Transaction> outgoing = db.transactions.SelectAll($"WHERE from_id={account.account_id}");
            List<Transaction> incoming = db.transactions.SelectAll($"WHERE to_id={account.account_id}");
            Dictionary<string, double> balance = new Dictionary<string, double>();
            Dictionary<string, bool> paidInterest = new Dictionary<string, bool>();

            foreach (Transaction income in incoming)
            {
                string day = income.timestamp.Date.ToString();

                if (balance.ContainsKey(day))
                {
                    balance[day] += income.amount;
                }
                else
                {
                    balance.Add(day, income.amount);
                }
            }
            foreach (Transaction outcome in outgoing)
            {
                string day = outcome.timestamp.Date.ToString();
                if (outcome.type == "credit_interest" && !paidInterest.ContainsKey(day))
                {
                    string key = DateTime.Parse(day).AddDays(-1).ToString();
                    paidInterest.Add(key, true);
                }
                if (balance.ContainsKey(day))
                {
                    balance[day] -= outcome.amount;
                }
                else
                {
                    balance.Add(day, -1*outcome.amount);
                }
            }

            double prevBalance = 0;

            KeyValuePair<string, double> firstBalance = balance.First();
            DateTime first = DateTime.Parse(firstBalance.Key);
            int difference = date.Date.Subtract(first).Days;

            int counter = interestFreePeriod;

            List<Transaction> interests = new List<Transaction>();
            
            for (int i = 0; i <= difference; i++)
            {
                DateTime day = first.AddDays(i);
                string key = day.Date.ToString();
                double account_change = balance.ContainsKey(key) ? balance[key] : 0;
                prevBalance += account_change;
                if (prevBalance >= 0) { 
                    counter = interestFreePeriod;
                    continue;
                };
                double interest = prevBalance * creditRate * 0.0028;
                prevBalance += interest;
                if (day < date.Date && !paidInterest.ContainsKey(key))
                {
                    DateTime timestamp = day.AddDays(1);
                    Transaction t = new Transaction(0, account.account_id, 0, -1 * interest, "credit_interest", timestamp);
                    interests.Add(t);
                    counter -= 1;

                    if (counter < 0)
                    {
                        foreach (Transaction j in interests)
                        {
                            db.transactions.Insert(j);
                            db.logs.Insert(new Log(0, $"Credit payment from {account.account_id}", "INFO", DateTime.Now));
                        }
                        interests.Clear();
                    }
                }
            }
        }

        User Login()
        {
            while (true)
            {
                string username = Readline("Enter username:");
                string password = Readline("Enter password:");

                User user = db.users.Select(username);
                if (user != null && Auth.VerifyPassword(password, user.password))
                {
                    string condition = $"WHERE uid={user.uid}";
                    List<Account> accounts = db.accounts.SelectAll(condition);
                    foreach (Account account in accounts)
                    {
                        CalculateInterest(account);
                        CalculateCredit(account);
                    }
                    db.logs.Insert(new Log(0, $"Succesful login by {user.uid}", "INFO", DateTime.Now));
                    return user;
                }
                Console.WriteLine("\nSomething is wrong, try again\n");
                db.logs.Insert(new Log(0, $"Failed login", "ERROR", DateTime.Now));
            }
        }

        void Signin()
        {
            while (true)
            {
                Console.Clear();

                Console.WriteLine("Create user account:\n");
                string username = Readline("Enter username:");
                string password = Readline("Enter password:");
                string hashed = Auth.HashPassword(password);
                string role = ReadOptions("Enter role (user, banker, admin)", new string[]{"user", "banker", "admin"});

                User exists = db.users.Select(username);
                if (exists == null)
                {
                    int id = db.users.Insert(new User(username, hashed, role, 0));
                    db.logs.Insert(new Log(0, $"Created user {id}", "INFO", DateTime.Now));
                    return;
                }
                Console.WriteLine("A user with that username already exists");
                db.logs.Insert(new Log(0, $"Failed to create user", "ERROR", DateTime.Now));
            }
        }

        double CalculateBalance(Account account)
        {
            List<Transaction> outgoing = db.transactions.SelectAll($"WHERE from_id={account.account_id}");
            List<Transaction> incoming = db.transactions.SelectAll($"WHERE to_id={account.account_id}");
            double balance = 0;

            foreach (Transaction income in incoming)
            {
                balance += income.amount;
            }
            foreach (Transaction outcome in outgoing)
            {
                balance -= outcome.amount;
            }
            return Math.Round(balance, 5);
        }

        void AccountTable(int account_id=0) {
            ConsoleTable table = new ConsoleTable("ID", "Account", "Type", "Balance", "Status");

            string condition = $"WHERE uid={user.uid}";
            string single_condition = "";

            if (account_id != 0)
            {
                single_condition = $"WHERE account_id={account_id}";
                condition += $" AND account_id={account_id}";
            }

            List<Account> accounts = db.accounts.SelectAll(user.role == "banker" ? single_condition : condition);

            foreach (Account account in accounts)
            {
                double balance = CalculateBalance(account);
                table.AddRow(account.account_id, account.name, account.type, balance, balance > 0 ? "Good" : "Bad");
            }
            table.Write(Format.Alternative);
        }

        void AccountsHome()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"Accounts:\n");

                AccountTable();

                Console.WriteLine("Options:");
                Console.WriteLine("1) View account details");
                Console.WriteLine("2) Create account");
                Console.WriteLine("3) Go back");

                int option = ReadInt("Enter option (number):");
                switch (option)
                {
                    case 1:
                        Account account = ReadAccount("Enter account (id):");
                        account.balance = CalculateBalance(account);
                        AccountView(account);
                        break;
                    case 2:
                        CreateAccount();
                        break;
                    case 3:
                        return;
                    default:
                        break;
                }
            }
        }
        void AccountView(Account account)
        {
            while (true)
            {
                Console.WriteLine($"\nAccount {account.name}:\n");

                AccountTable(account.account_id);

                Console.WriteLine("Options:");
                Console.WriteLine("1) New transaction");
                Console.WriteLine("2) Delete account");
                Console.WriteLine("3) View transactions");
                Console.WriteLine("4) Go back");

                int option = ReadInt("Enter option (number):");
                switch (option)
                {
                    case 1:
                        Transaction(account);
                        account.balance = CalculateBalance(account);
                        break;
                    case 2:
                        bool deleted = DeleteAccount(account);
                        if (deleted)
                        {
                            return;
                        }
                        break;
                    case 3:
                        Console.WriteLine($"\nTransactions:\n");
                        TransactionTable(account);
                        break;
                    case 4:
                        return;
                    default:
                        break;
                }
            }
        }

        void CreateAccount()
        {
            Console.Clear();

            Console.WriteLine("Create account:\n");

            string name = Readline("Enter account name:");
            string type = ReadOptions("Enter account type (Savings, Normal, Credit):", new string[]{"Savings", "Normal", "Credit"});
            double balance = ReadDouble("Enter balance (for testing):");

            Account account = new Account(0, user.uid, name, "Good", type, 0);

            int inserted = db.accounts.Insert(account);
            db.transactions.Insert(new Transaction(0, 0, inserted, balance, "transaction", date));
            db.logs.Insert(new Log(0, $"Created bank account {inserted}", "INFO", DateTime.Now));
        }
        bool DeleteAccount(Account account)
        {
            if (account.uid != user.uid || user.role == "banker")
            {
                Console.WriteLine("You do not own this account");
                db.logs.Insert(new Log(0, $"Failed to delete bank account {account.account_id}", "ERROR", DateTime.Now));
                return false;
            }  
            else if (account.balance != 0)
            {
                Console.WriteLine("Account balance isn't zero");
                db.logs.Insert(new Log(0, $"Failed to delete bank account {account.account_id}", "ERROR", DateTime.Now));
                return false;
            }

            db.accounts.Delete(account.account_id);
            db.logs.Insert(new Log(0, $"Deleted bank account {account.account_id}", "INFO", DateTime.Now));
            return true;
        }

        void Transaction(Account from_account)
        {
            Account to_account = ReadAccount("To account (id):", false);

            while (true)
            {
                double amount = ReadDouble("Enter amount:");
                if (amount <= 0)
                {
                    Console.WriteLine("Ammount has to be higher than zero");
                    db.logs.Insert(new Log(0, $"Failed transaction from {from_account.account_id} to {to_account.account_id}", "ERROR", DateTime.Now));
                    continue;
                } else if (from_account.balance - amount < 0 && from_account.type != "Credit")
                {
                    Console.WriteLine("Insufficient funds");
                    db.logs.Insert(new Log(0, $"Failed transaction from {from_account.account_id} to {to_account.account_id}", "ERROR", DateTime.Now));
                    continue;
                }
                db.transactions.Insert(new Transaction(0, from_account.account_id, to_account.account_id, amount, "transaction", date));
                db.logs.Insert(new Log(0, $"Succesful transaction from {from_account.account_id} to {to_account.account_id}", "INFO", DateTime.Now));
                break;
            }
        }

        void TransactionTable(Account account)
        {
            ConsoleTable table = new ConsoleTable("ID", "From", "To", "Amount", "Type", "Timestamp");

            string condition = $"WHERE from_id={account.account_id} OR to_id={account.account_id}";

            List<Transaction> transactions = db.transactions.SelectAll(condition);

            foreach (Transaction transaction in transactions)
            {
                table.AddRow(transaction.transaction_id, transaction.from_id, transaction.to_id, transaction.to_id == account.account_id ? $"+{transaction.amount}" : -transaction.amount, transaction.type, transaction.timestamp);
            }
            table.Write(Format.Alternative);
        }

        int Home()
        {
            Console.WriteLine($"Welcome {user.username}!\n");

            Console.WriteLine("Options:");
            Console.WriteLine("1) View accounts");
            Console.WriteLine("2) Logout");
            Console.WriteLine("3) Quit");

            int option = ReadInt("Enter option (number):");
            return option;
        }

        void LogTable()
        {
            ConsoleTable table = new ConsoleTable("ID", "Title", "Severity", "Timestamp");

            List<Log> logs = db.logs.SelectAll("");

            foreach (Log log in logs)
            {
                table.AddRow(log.log_id, log.title, log.severity, log.timestamp);
            }
            table.Write(Format.Alternative);
        }

        int AdminHome()
        {
            Console.WriteLine($"Welcome {user.username}!\n");

            Console.WriteLine("Options:");
            Console.WriteLine("1) Create user account");
            Console.WriteLine("2) View logs");
            Console.WriteLine("3) Logout");
            Console.WriteLine("4) Quit");

            int option = ReadInt("Enter option (number):");
            return option;
        }

        public void Start()
        {
            while (true)
            {
                if (user == null)
                {
                    user = Login();
                }
                if (user.role != "admin")
                {
                    int option = Home();

                    switch (option)
                    {
                        case 1:
                            AccountsHome();
                            break;
                        case 2:
                            user = null;
                            break;
                        case 3:
                            Console.WriteLine("Goodbye!");
                            return;
                        default:
                            break;
                    }
                } else
                {
                    int option = AdminHome();

                    switch (option)
                    {
                        case 1:
                            Signin();
                            break;
                        case 2:
                            Console.WriteLine($"\nLogs:\n");
                            LogTable();
                            break;
                            break;
                        case 3:
                            user = null;
                            break;
                        case 4:
                            Console.WriteLine("Goodbye!");
                            return;
                        default:
                            break;
                    }
                }
            }
        }
    }
}