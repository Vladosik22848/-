using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace Kursovaya.Views
{
    public partial class AddClientWindow : Window
    {
        private readonly user149_dbEntities _db = new user149_dbEntities();
        public Clients NewClient { get; private set; }

        private const int MaxFullName = 255;
        private const int MaxEmail = 255;
        private const int MaxPhone = 32;

        public AddClientWindow()
        {
            InitializeComponent();
            Closed += (s, e) => _db.Dispose();
            try { _db.Clients.Load(); } catch {  }
        }

        private void OnChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ValidateForm();

        private void ValidateForm()
        {
            var fullName = (FullNameBox.Text ?? "").Trim();
            var email = (EmailBox.Text ?? "").Trim();

            bool emailOk = string.IsNullOrWhiteSpace(email) || IsValidEmail(email);
            AddButton.IsEnabled =
                !string.IsNullOrWhiteSpace(fullName) &&
                fullName.Length <= MaxFullName &&
                (string.IsNullOrWhiteSpace(email) || email.Length <= MaxEmail) &&
                emailOk;

            EmailBox.BorderBrush = emailOk ? System.Windows.Media.Brushes.Gray : System.Windows.Media.Brushes.Red;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            }
            catch { return false; }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var fullName = (FullNameBox.Text ?? "").Trim();
            var phone = (PhoneBox.Text ?? "").Trim();
            var email = (EmailBox.Text ?? "").Trim();
            var notes = (NotesBox.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Введите ФИО клиента.", "Клиент", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (fullName.Length > MaxFullName)
            {
                MessageBox.Show($"ФИО слишком длинное (максимум {MaxFullName}).", "Клиент", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
            {
                MessageBox.Show("Введите корректный Email.", "Клиент", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (email.Length > MaxEmail)
            {
                MessageBox.Show($"Email слишком длинный (максимум {MaxEmail}).", "Клиент", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (phone.Length > MaxPhone)
            {
                MessageBox.Show($"Телефон слишком длинный (максимум {MaxPhone}).", "Клиент", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {


                NewClient = new Clients
                {
                    FullName = fullName,
                    Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                    CreatedAt = DateTime.Now
                };
                _db.Clients.Add(NewClient);
                _db.SaveChanges();

                DialogResult = true;
                Close();
            }
            catch (DbUpdateException dbEx)
            {
                MessageBox.Show(BuildSqlErrorDetails(dbEx), "Клиент", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при добавлении клиента:\n" + ex, "Клиент",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildSqlErrorDetails(Exception ex)
        {
            var sb = new StringBuilder();
            int level = 0;
            var cur = ex;
            while (cur != null && level < 10)
            {
                sb.AppendLine($"[{level}] {cur.GetType().Name}: {cur.Message}");
                if (cur is SqlException sql)
                {
                    sb.AppendLine($" SqlNumber: {sql.Number}");
                    foreach (SqlError err in sql.Errors)
                        sb.AppendLine($"  - {err.Number}: {err.Message}");
                }
                cur = cur.InnerException;
                level++;
            }
            return sb.ToString();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}