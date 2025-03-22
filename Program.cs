using System.Security.Cryptography;

namespace Banka
{
    internal class Program
    {
        static void Main(string[] args)
        {
            DB db = new DB();

            if (db.users.Select("admin") == null)
            {
                db.users.Insert(new User("admin", Auth.HashPassword("admin"), "admin", 0));
            }

            Interface ui = new Interface(db);
            ui.Start();
        }
    }
}