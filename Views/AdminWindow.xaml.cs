using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Windows;
using Kursovaya;
using Kursovaya.Security;

namespace Kursovaya.Views
{
    public partial class AdminWindow : Window
    {
        private readonly Users _currentUser;
        private readonly user149_dbEntities _db = new user149_dbEntities();

        public AdminWindow(Users currentUser)
        {
            _currentUser = currentUser;
            InitializeComponent();
            Loaded += AdminWindow_Loaded;
            Closed += (s, e) => _db.Dispose();
        }

        private void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _db.Users.Load();
            _db.Vehicles.Load();
            _db.Clients.Load();
            _db.Sales.Load();
            _db.Reservations.Load();

            UsersGrid.ItemsSource = _db.Users.Local;
            UpdateStatistics();
        }

        private void UpdateStatistics()
        {
            var totalVehicles = _db.Vehicles.Count();
            var availableVehicles = _db.Vehicles.Count(v => v.Status == "В наличии");
            var reservedVehicles = _db.Vehicles.Count(v => v.Status == "В резерве");
            var soldVehicles = _db.Vehicles.Count(v => v.Status == "Продано");
            var totalClients = _db.Clients.Count();
            var totalUsers = _db.Users.Count();

            if (StatisticsText != null)
            {
                StatisticsText.Text = $"Всего автомобилей: {totalVehicles}\n" +
                                      $"В наличии: {availableVehicles}\n" +
                                      $"В резерве: {reservedVehicles}\n" +
                                      $"Продано: {soldVehicles}\n" +
                                      $"Всего клиентов: {totalClients}\n" +
                                      $"Всего пользователей: {totalUsers}";
            }

            if (TotalVehiclesText != null) TotalVehiclesText.Text = totalVehicles.ToString();
            if (AvailableVehiclesText != null) AvailableVehiclesText.Text = availableVehicles.ToString();
            if (ReservedVehiclesText != null) ReservedVehiclesText.Text = reservedVehicles.ToString();
            if (SoldVehiclesText != null) SoldVehiclesText.Text = soldVehicles.ToString();
            if (TotalClientsText != null) TotalClientsText.Text = totalClients.ToString();
            if (TotalUsersText != null) TotalUsersText.Text = totalUsers.ToString();

            if (UserInfoText != null)
                UserInfoText.Text = $"Пользователь: {_currentUser.Email} ({_currentUser.Role})";
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new AddUserWindow { Owner = this };

            if (addWindow.ShowDialog() == true && addWindow.NewUser != null)
            {
                _db.Users.Load();
                UsersGrid.ItemsSource = _db.Users.Local;
                UsersGrid.SelectedItem = addWindow.NewUser;
                UsersGrid.ScrollIntoView(addWindow.NewUser);
                UpdateStatistics();
            }
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var u = UsersGrid.SelectedItem as Users;
            if (u == null) return;

            if (u.Id == _currentUser.Id)
            {
                MessageBox.Show("Нельзя удалить пользователя, под которым вы вошли.", "Удаление пользователя",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var salesCount = _db.Sales.Count(s => s.SellerId == u.Id);
            var resCount = _db.Reservations.Count(r => r.ReservedBy == u.Id);
            var deps = salesCount + resCount;

            if (deps > 0)
            {
                var msg =
                    $"У пользователя {u.Email} есть связанные записи:\n" +
                    $"- Продажи: {salesCount}\n" +
                    $"- Резервы: {resCount}\n\n" +
                    "Выберите действие:\n" +
                    "Да — переназначить все записи на текущего пользователя и удалить.\n" +
                    "Нет — пометить пользователя как неактивного (не удалять).\n" +
                    "Отмена — отменить операцию.";
                var answer = MessageBox.Show(msg, "Удаление пользователя",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (answer == MessageBoxResult.Cancel) return;

                if (answer == MessageBoxResult.No)
                {
                    u.IsActive = false;
                    if (TrySaveDetailed("Пользователи"))
                        MessageBox.Show("Пользователь деактивирован (IsActive = false).", "Пользователи",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (answer == MessageBoxResult.Yes)
                {
                    var current = _db.Users.Local.FirstOrDefault(x => x.Id == _currentUser.Id) ?? _db.Users.Find(_currentUser.Id);
                    if (current == null)
                    {
                        MessageBox.Show("Не найден текущий пользователь для переназначения.", "Пользователи",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    using (var tr = _db.Database.BeginTransaction())
                    {
                        try
                        {
                            foreach (var s in _db.Sales.Where(s => s.SellerId == u.Id))
                                s.SellerId = current.Id;

                            foreach (var r in _db.Reservations.Where(r => r.ReservedBy == u.Id))
                                r.ReservedBy = current.Id;

                            _db.SaveChanges();

                            _db.Users.Remove(u);
                            _db.SaveChanges();

                            tr.Commit();

                            UsersGrid.ItemsSource = _db.Users.Local;
                            UpdateStatistics();
                            MessageBox.Show("Записи переназначены, пользователь удалён.", "Пользователи",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (DbUpdateException dbEx)
                        {
                            tr.Rollback();
                            MessageBox.Show(BuildSqlErrorDetails(dbEx), "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        catch (Exception ex)
                        {
                            tr.Rollback();
                            MessageBox.Show("Ошибка при удалении: " + ex, "Пользователи",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }

                    return;
                }
            }


            if (MessageBox.Show($"Удалить пользователя {u.Email}?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    _db.Users.Remove(u);
                    _db.SaveChanges();
                    UpdateStatistics();
                }
                catch (DbUpdateException dbEx)
                {
                    MessageBox.Show(BuildSqlErrorDetails(dbEx), "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при удалении: " + ex.Message, "Пользователи",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveUsers_Click(object sender, RoutedEventArgs e)
        {
            if (TrySaveDetailed("Пользователи"))
            {
                UpdateStatistics();
                MessageBox.Show("Изменения сохранены.", "Пользователи",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            var u = UsersGrid.SelectedItem as Users;
            if (u == null)
            {
                MessageBox.Show("Выберите пользователя.", "Сброс пароля",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Сбросить пароль для {u.Email} на '12345'?",
                "Сброс пароля", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                u.PasswordHash = PasswordHasher.Hash("12345");
                if (TrySaveDetailed("Сброс пароля"))
                    MessageBox.Show("Пароль сброшен.", "Сброс пароля",
                        MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите выйти?", "Выход",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                Close();
            }
        }

        private bool TrySaveDetailed(string title)
        {
            try
            {
                _db.SaveChanges();
                return true;
            }
            catch (DbUpdateException dbEx)
            {
                MessageBox.Show(BuildSqlErrorDetails(dbEx), title, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения:\n" + ex, title, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
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
    }
}