using System;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Kursovaya;
using Kursovaya.Security;

namespace Kursovaya.Views
{
    public partial class AddUserWindow : Window
    {
        private readonly user149_dbEntities _db = new user149_dbEntities();
        public Users NewUser { get; private set; }

        public AddUserWindow()
        {
            InitializeComponent();
            Closed += (s, e) => _db.Dispose();
            
            // Загружаем пользователей для проверки
            try
            {
                _db.Users.Load();
            }
            catch
            {
                // Игнорируем ошибки загрузки
            }
            
            EmailBox.Focus();
            
            // Инициализация валидации
            ValidateForm();
        }

        private void EmailBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateForm();
        }

        private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            var passwordBox = sender as System.Windows.Controls.PasswordBox;
            if (passwordBox != null && PasswordHintText != null)
            {
                if (string.IsNullOrWhiteSpace(passwordBox.Password))
                {
                    PasswordHintText.Text = "По умолчанию: 12345 (можно изменить)";
                    PasswordHintText.Foreground = System.Windows.Media.Brushes.Gray;
                }
                else if (passwordBox.Password.Length < 5)
                {
                    PasswordHintText.Text = "⚠️ Пароль слишком короткий (минимум 5 символов)";
                    PasswordHintText.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    PasswordHintText.Text = "✅ Пароль установлен";
                    PasswordHintText.Foreground = System.Windows.Media.Brushes.Green;
                }
            }
        }

        private void ValidateForm()
        {
            var email = EmailBox.Text?.Trim();
            var isValid = !string.IsNullOrWhiteSpace(email) && 
                         IsValidEmail(email) && 
                         !_db.Users.Local.Any(u => u.Email == email);

            AddButton.IsEnabled = isValid;

            if (string.IsNullOrWhiteSpace(email))
            {
                EmailBox.BorderBrush = System.Windows.Media.Brushes.Gray;
            }
            else if (!IsValidEmail(email))
            {
                EmailBox.BorderBrush = System.Windows.Media.Brushes.Red;
            }
            else if (_db.Users.Local.Any(u => u.Email == email))
            {
                EmailBox.BorderBrush = System.Windows.Media.Brushes.Orange;
            }
            else
            {
                EmailBox.BorderBrush = System.Windows.Media.Brushes.Green;
            }
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var email = EmailBox.Text?.Trim();
            var phone = PhoneBox.Text?.Trim();
            var password = PasswordBox.Password;
            
            // Получаем роль из ComboBox
            var roleItem = RoleCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
            var role = roleItem?.Tag?.ToString();
            
            if (string.IsNullOrWhiteSpace(role))
            {
                MessageBox.Show("Выберите роль пользователя.", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                RoleCombo.Focus();
                return;
            }
            
            var isActive = IsActiveCheckBox.IsChecked ?? true;

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Введите Email.", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailBox.Focus();
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.Show("Введите корректный Email.", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailBox.Focus();
                return;
            }

            // Проверяем существование пользователя в базе данных
            try
            {
                _db.Users.Load();
                if (_db.Users.Local.Any(u => u.Email == email))
                {
                    MessageBox.Show("Пользователь с таким Email уже существует.", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    EmailBox.Focus();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке пользователя:\n{ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                password = "12345";
            }

            // Проверяем длину полей
            if (email.Length > 255)
            {
                MessageBox.Show("Email слишком длинный (максимум 255 символов).", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                EmailBox.Focus();
                return;
            }

            if (!string.IsNullOrWhiteSpace(phone) && phone.Length > 32)
            {
                MessageBox.Show("Телефон слишком длинный (максимум 32 символа).", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                PhoneBox.Focus();
                return;
            }

            if (role.Length > 20)
            {
                MessageBox.Show("Роль слишком длинная (максимум 20 символов).", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверяем, что телефон уникален (если указан)
            if (!string.IsNullOrWhiteSpace(phone))
            {
                try
                {
                    _db.Users.Load();
                    if (_db.Users.Local.Any(u => u.Phone == phone && !string.IsNullOrEmpty(u.Phone)))
                    {
                        MessageBox.Show("Пользователь с таким телефоном уже существует.", "Ошибка", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        PhoneBox.Focus();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при проверке телефона:\n{ex.Message}", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Проверяем, что роль не пустая
            if (string.IsNullOrWhiteSpace(role))
            {
                MessageBox.Show("Выберите роль пользователя.", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Хешируем пароль
                string passwordHash = PasswordHasher.Hash(password);
                
                // Проверяем длину хеша
                if (passwordHash.Length > 255)
                {
                    MessageBox.Show("Ошибка: хеш пароля слишком длинный.", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Нормализуем роль (убираем пробелы, проверяем регистр)
                string normalizedRole = role.Trim();
                
                // Проверяем, что роль соответствует разрешенным значениям
                if (normalizedRole != "Admin" && normalizedRole != "Employee" && normalizedRole != "Warehouse")
                {
                    MessageBox.Show($"Недопустимая роль: {normalizedRole}\n\nРазрешенные роли: Admin, Employee, Warehouse", 
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                NewUser = new Users
                {
                    Email = email,
                    Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    Role = normalizedRole, // Используем нормализованную роль
                    IsActive = isActive,
                    CreatedAt = DateTime.Now,
                    PasswordHash = passwordHash
                };

                _db.Users.Add(NewUser);
                _db.SaveChanges();

                MessageBox.Show($"Пользователь {email} успешно добавлен!\nРоль: {role}\nПароль: {password}", 
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException dbEx)
            {
                string errorMessage = "Ошибка при добавлении пользователя в базу данных.";
                
                // Получаем все вложенные исключения
                Exception inner = dbEx;
                int level = 0;
                while (inner != null && level < 5)
                {
                    var sqlEx = inner as System.Data.SqlClient.SqlException;
                    if (sqlEx != null)
                    {
                        // Обрабатываем специфичные ошибки SQL Server
                        switch (sqlEx.Number)
                        {
                            case 2601: // Нарушение уникального индекса (duplicate key)
                            case 2627: // Нарушение ограничения PRIMARY KEY или UNIQUE
                                errorMessage = "Пользователь с таким Email или телефоном уже существует в базе данных.";
                                break;
                            case 515: // Cannot insert the value NULL into column
                                errorMessage = "Ошибка: одно из обязательных полей не заполнено.";
                                break;
                            case 8152: // String or binary data would be truncated
                                errorMessage = "Ошибка: одно из полей слишком длинное.";
                                break;
                            default:
                                errorMessage = $"Ошибка базы данных (код {sqlEx.Number}):\n{sqlEx.Message}";
                                if (sqlEx.Errors != null && sqlEx.Errors.Count > 0)
                                {
                                    errorMessage += $"\n\nДетали:\n";
                                    foreach (System.Data.SqlClient.SqlError err in sqlEx.Errors)
                                    {
                                        errorMessage += $"- {err.Message}\n";
                                    }
                                }
                                break;
                        }
                        break;
                    }
                    inner = inner.InnerException;
                    level++;
                }
                
                if (inner != null && !(inner is System.Data.SqlClient.SqlException))
                {
                    errorMessage = $"Ошибка: {inner.Message}";
                }
                
                MessageBox.Show(errorMessage, 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException valEx)
            {
                string errorMessage = "Ошибка валидации данных:\n\n";
                foreach (var validationError in valEx.EntityValidationErrors)
                {
                    foreach (var error in validationError.ValidationErrors)
                    {
                        errorMessage += $"- {error.PropertyName}: {error.ErrorMessage}\n";
                    }
                }
                MessageBox.Show(errorMessage, 
                    "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка при добавлении пользователя:\n{ex.Message}";
                
                Exception inner = ex.InnerException;
                int level = 0;
                while (inner != null && level < 3)
                {
                    errorMessage += $"\n\nДетали (уровень {level + 1}): {inner.Message}";
                    inner = inner.InnerException;
                    level++;
                }
                
                MessageBox.Show(errorMessage, 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void EmailBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                PhoneBox.Focus();
            }
        }

        private void PasswordBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && AddButton.IsEnabled)
            {
                Add_Click(sender, e);
            }
        }
    }
}

