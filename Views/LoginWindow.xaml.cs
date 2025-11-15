using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using Kursovaya.Security;

namespace Kursovaya.Views
{
    public partial class LoginWindow : Window
    {
        private readonly user149_dbEntities _db = new user149_dbEntities();
        private const string SalonClientName = "[СИСТЕМА] Автосалон";

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += LoginWindow_Loaded;
            Closed += (s, e) => _db.Dispose();
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureDefaultUsersExist();
        }

        private void EnsureDefaultUsersExist()
        {
            _db.Users.Load();
            _db.Clients.Load();

      
            if (!_db.Users.Local.Any(u => u.Email == "admin@example.com"))
            {
                _db.Users.Add(new Users
                {
                    Email = "admin@example.com",
                    Phone = "+7 (999) 123-45-67",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    PasswordHash = PasswordHasher.Hash("12345")
                });
            }

            if (!_db.Users.Local.Any(u => u.Email == "employee1@example.com"))
            {
                _db.Users.Add(new Users
                {
                    Email = "employee1@example.com",
                    Phone = "+7 (999) 234-56-78",
                    Role = "Employee",
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    PasswordHash = PasswordHasher.Hash("12345")
                });
            }

            if (!_db.Users.Local.Any(u => u.Email == "warehouse1@example.com"))
            {
                _db.Users.Add(new Users
                {
                    Email = "warehouse1@example.com",
                    Phone = "+7 (999) 456-78-90",
                    Role = "Warehouse",
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    PasswordHash = PasswordHasher.Hash("12345")
                });
            }

            if (!_db.Clients.Local.Any(c => c.FullName == SalonClientName))
            {
                _db.Clients.Add(new Clients
                {
                    FullName = SalonClientName,
                    CreatedAt = DateTime.Now
                });
            }

            try
            {
                _db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при создании дефолтных данных: {ex.Message}");
            }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var email = LoginBox.Text?.Trim();
            var pass = PasswordBox.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Введите Email и пароль.", "Вход", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var user = _db.Users.FirstOrDefault(u => u.IsActive && u.Email == email);
            if (user == null)
            {
                MessageBox.Show("Неверный Email или пароль.", "Вход", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var stored = user.PasswordHash ?? "";
            bool ok = PasswordHasher.Verify(pass, stored);
            if (!ok)
            {
                MessageBox.Show("Неверный Email или пароль.", "Вход", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!stored.StartsWith("PBKDF2$", StringComparison.Ordinal))
            {
                user.PasswordHash = PasswordHasher.Hash(pass);
                try { _db.SaveChanges(); } catch {  }
            }

            Window next = null;
            switch ((user.Role ?? "").Trim().ToLowerInvariant())
            {
                case "admin":
                    next = new AdminWindow(user);
                    break;
                case "employee":
                    next = new EmployeeWindow(user);
                    break;
                case "warehouse":
                    next = new WarehouseWindow(user);
                    break;
                default:
                    MessageBox.Show($"Роль '{user.Role}' не поддерживается.");
                    return;
            }

            next.Show();
            Close();
        }
    }
}