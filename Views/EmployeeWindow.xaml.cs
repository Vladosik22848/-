using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;                     // ВАЖНО: для LINQ и Contains
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Kursovaya.Views
{
    public partial class EmployeeWindow : Window
    {
        private readonly Users _currentUser;
        private readonly user149_dbEntities _db = new user149_dbEntities();

        private string[] _allowedStatuses;
        private const string SalonClientName = "[СИСТЕМА] Автосалон";

        public string[] VehicleStatusList => _allowedStatuses ?? new string[0];

        public EmployeeWindow(Users currentUser)
        {
            _currentUser = currentUser;
            InitializeComponent();

            Loaded += EmployeeWindow_Loaded;
            Closed += (s, e) => _db.Dispose();
        }

        public ObservableCollection<Clients> ClientsLookup => _db.Clients.Local;
        public ObservableCollection<Vehicles> VehiclesLookup => _db.Vehicles.Local;

        private void EmployeeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _db.Clients.Load();
            _db.Vehicles.Load();
            _db.Sales.Load();
            _db.Reservations.Load();
            _db.Users.Load();

            _allowedStatuses = GetAllowedVehicleStatuses();

            BindAll();

            if (EmployeeInfoText != null)
                EmployeeInfoText.Text = $"Пользователь: {_currentUser.Email} ({_currentUser.Role})";
        }

        private void BindAll()
        {
            ClientsGrid.ItemsSource = ClientsLookup;

            VehiclesGrid.ItemsSource = VehiclesLookup
                .Where(v => v.Status == StatusAvailable || v.Status == StatusReserved)
                .ToList();

            SearchResultsGrid.ItemsSource = VehiclesLookup;

            SaleVehicleCombo.ItemsSource = VehiclesLookup.Where(v => v.Status == StatusAvailable).ToList();
            SaleClientCombo.ItemsSource = ClientsLookup;

            SoldVehiclesGrid.ItemsSource = VehiclesLookup.Where(v => v.Status == StatusSold).ToList();

            UpdateSalesInfo();
        }

        // ===== Clients =====
        private void AddClient_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new AddClientWindow { Owner = this };
            if (wnd.ShowDialog() == true && wnd.NewClient != null)
            {
                _db.Clients.Load();
                var added = ClientsLookup.FirstOrDefault(c => c.Id == wnd.NewClient.Id);
                ClientsGrid.ItemsSource = ClientsLookup;
                if (added != null)
                {
                    ClientsGrid.SelectedItem = added;
                    ClientsGrid.ScrollIntoView(added);
                }
                SaleClientCombo.ItemsSource = ClientsLookup;
            }
        }

        private void DeleteClient_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsGrid.SelectedItem is Clients c)
            {
                if (string.Equals(c.FullName ?? "", SalonClientName, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Нельзя удалить системного клиента '[СИСТЕМА] Автосалон'.", "Клиенты",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var salesCount = _db.Sales.Count(s => s.ClientId == c.Id);
                var allVehsByClient = _db.Vehicles.Where(v => v.OwnerId == c.Id).ToList();
                var vehCount = allVehsByClient.Count;
                var soldVehCount = allVehsByClient.Count(v => v.Status == StatusSold);
                var resCount = _db.Reservations.Count(r => r.ClientId == c.Id);

                if (salesCount > 0 || soldVehCount > 0)
                {
                    MessageBox.Show(
                        "Удаление невозможно: у клиента есть оформленные продажи и/или проданные автомобили.\n" +
                        "- Продажи: " + salesCount + "\n" +
                        "- Проданных авто: " + soldVehCount + "\n\n" +
                        "Рекомендуется оставить клиента в базе.",
                        "Клиенты", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (resCount > 0)
                {
                    if (MessageBox.Show(
                        $"У клиента есть бронирования: {resCount}.\nУдалить бронирования и продолжить?",
                        "Клиенты", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        var ress = _db.Reservations.Where(r => r.ClientId == c.Id).ToList();
                        foreach (var r in ress)
                            _db.Reservations.Remove(r);
                    }
                    else
                    {
                        return;
                    }
                }

                if (vehCount > 0)
                {
                    var unsold = allVehsByClient.Where(v => v.Status != StatusSold).ToList();
                    if (unsold.Count > 0)
                    {
                        if (MessageBox.Show(
                            $"У клиента числятся автомобили (не проданы): {unsold.Count}.\n" +
                            "Переназначить владельца этих авто на '[СИСТЕМА] Автосалон' и продолжить удаление клиента?",
                            "Клиенты", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            var salon = GetOrCreateSalonClient();
                            foreach (var v in unsold)
                                v.OwnerId = salon.Id;
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                if (MessageBox.Show($"Удалить клиента {c.FullName}?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                try
                {
                    _db.Clients.Remove(c);
                    _db.SaveChanges();
                    BindAll();
                }
                catch (DbUpdateException dbEx)
                {
                    MessageBox.Show(BuildSqlErrorDetails(dbEx), "Клиенты", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при удалении клиента:\n" + ex, "Клиенты",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveClients_Click(object sender, RoutedEventArgs e)
        {
            TrySaveDetailed("Клиенты");
            BindAll();
        }

        // ===== Vehicles =====
        private void AddVehicle_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new AddVehicleWindow { Owner = this };
            if (wnd.ShowDialog() == true && wnd.NewVehicle != null)
            {
                _db.Vehicles.Load();
                var added = VehiclesLookup.FirstOrDefault(v => v.Id == wnd.NewVehicle.Id);

                VehiclesGrid.ItemsSource = VehiclesLookup
                    .Where(v => v.Status == StatusAvailable || v.Status == StatusReserved)
                    .ToList();

                if (added != null)
                {
                    VehiclesGrid.SelectedItem = added;
                    VehiclesGrid.ScrollIntoView(added);
                }

                SaleVehicleCombo.ItemsSource = VehiclesLookup.Where(v => v.Status == StatusAvailable).ToList();
                SoldVehiclesGrid.ItemsSource = VehiclesLookup.Where(v => v.Status == StatusSold).ToList();
                SearchResultsGrid.ItemsSource = VehiclesLookup;
                UpdateSalesInfo();
            }
        }

        private void DeleteVehicle_Click(object sender, RoutedEventArgs e)
        {
            if (VehiclesGrid.SelectedItem is Vehicles v)
            {
                if (MessageBox.Show($"Удалить автомобиль {v.Brand} {v.Model}?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _db.Vehicles.Remove(v);
                    if (TrySaveDetailed("Автомобили"))
                    {
                        VehiclesGrid.ItemsSource = VehiclesLookup
                            .Where(ve => ve.Status == StatusAvailable || ve.Status == StatusReserved).ToList();
                        SaleVehicleCombo.ItemsSource = VehiclesLookup.Where(ve => ve.Status == StatusAvailable).ToList();
                        SoldVehiclesGrid.ItemsSource = VehiclesLookup.Where(ve => ve.Status == StatusSold).ToList();
                        UpdateSalesInfo();
                    }
                }
            }
        }

        private void SaveVehicles_Click(object sender, RoutedEventArgs e)
        {
            if (TrySaveDetailed("Автомобили"))
            {
                VehiclesGrid.ItemsSource = VehiclesLookup
                    .Where(ve => ve.Status == StatusAvailable || ve.Status == StatusReserved).ToList();
                SaleVehicleCombo.ItemsSource = VehiclesLookup.Where(ve => ve.Status == StatusAvailable).ToList();
                SoldVehiclesGrid.ItemsSource = VehiclesLookup.Where(ve => ve.Status == StatusSold).ToList();
                UpdateSalesInfo();
            }
        }

        private void SellVehicle_Click(object sender, RoutedEventArgs e)
        {
            if (VehiclesGrid.SelectedItem is Vehicles v)
            {
                if (v.Status == StatusSold)
                {
                    MessageBox.Show("Этот автомобиль уже продан.", "Продажа",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (v.Status != StatusAvailable)
                {
                    MessageBox.Show($"Можно продать только автомобиль со статусом '{StatusAvailable}'.", "Продажа",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show($"Отметить автомобиль {v.Brand} {v.Model} как проданный?", "Продажа",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    v.Status = StatusSold;
                    if (TrySaveDetailed("Автомобили"))
                    {
                        VehiclesGrid.ItemsSource = VehiclesLookup
                            .Where(ve => ve.Status == StatusAvailable || ve.Status == StatusReserved).ToList();
                        SaleVehicleCombo.ItemsSource = VehiclesLookup.Where(ve => ve.Status == StatusAvailable).ToList();
                        SoldVehiclesGrid.ItemsSource = VehiclesLookup.Where(ve => ve.Status == StatusSold).ToList();
                        UpdateSalesInfo();
                    }
                }
            }
        }

        private void ReserveVehicle_Click(object sender, RoutedEventArgs e)
        {
            if (VehiclesGrid.SelectedItem is Vehicles v)
            {
                if (v.Status == StatusReserved)
                {
                    MessageBox.Show("Этот автомобиль уже зарезервирован.", "Резервирование",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (v.Status == StatusSold)
                {
                    MessageBox.Show("Нельзя зарезервировать проданный автомобиль.", "Резервирование",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show($"Зарезервировать автомобиль {v.Brand} {v.Model}?", "Резервирование",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    v.Status = StatusReserved;
                    if (TrySaveDetailed("Автомобили"))
                    {
                        VehiclesGrid.ItemsSource = VehiclesLookup
                            .Where(ve => ve.Status == StatusAvailable || ve.Status == StatusReserved).ToList();
                        SaleVehicleCombo.ItemsSource = VehiclesLookup.Where(ve => ve.Status == StatusAvailable).ToList();
                        UpdateSalesInfo();
                    }
                }
            }
        }

        // ===== Sales =====
        private void ProcessSale_Click(object sender, RoutedEventArgs e)
        {
            var vehicle = SaleVehicleCombo.SelectedItem as Vehicles;
            var client = SaleClientCombo.SelectedItem as Clients;

            if (vehicle == null || client == null)
            {
                MessageBox.Show("Выберите автомобиль и покупателя.", "Продажа",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (vehicle.Status != StatusAvailable)
            {
                MessageBox.Show($"Можно продать только автомобиль со статусом '{StatusAvailable}'.", "Продажа",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var seller = _db.Users.Local.FirstOrDefault(u => u.Id == _currentUser.Id) ?? _db.Users.Find(_currentUser.Id);
            if (seller == null)
            {
                MessageBox.Show("Не найден текущий пользователь-продавец.", "Продажа",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_db.Sales.Any(s => s.VehicleId == vehicle.Id))
            {
                MessageBox.Show("Продажа по этому автомобилю уже зарегистрирована.", "Продажа",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(
                    $"Оформить продажу автомобиля {vehicle.Brand} {vehicle.Model} клиенту {client.FullName}?",
                    "Оформление продажи", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            using (var tr = _db.Database.BeginTransaction())
            {
                try
                {
                    vehicle.Clients = client;
                    vehicle.Status = StatusSold;

                    var sale = new Sales
                    {
                        VehicleId = vehicle.Id,
                        ClientId = client.Id,
                        SellerId = seller.Id,
                        SalePrice = vehicle.Price,
                        SaleDate = DateTime.Now,
                        CreatedAt = DateTime.Now
                    };
                    _db.Sales.Add(sale);

                    _db.SaveChanges();
                    tr.Commit();

                    VehiclesGrid.ItemsSource = VehiclesLookup
                        .Where(v => v.Status == StatusAvailable || v.Status == StatusReserved).ToList();
                    SaleVehicleCombo.ItemsSource = VehiclesLookup.Where(v => v.Status == StatusAvailable).ToList();
                    SoldVehiclesGrid.ItemsSource = VehiclesLookup.Where(v => v.Status == StatusSold).ToList();
                    UpdateSalesInfo();

                    MessageBox.Show("Продажа оформлена успешно.", "Продажа",
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
                    MessageBox.Show("Ошибка при оформлении продажи:\n" + ex, "Продажа",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateSalesInfo()
        {
            var soldCount = VehiclesLookup.Count(v => v.Status == StatusSold);
            var salesCount = _db.Sales.Count();
            SalesInfoText.Text = $"Всего продано автомобилей: {soldCount}\nЗаписей в Sales: {salesCount}";
        }

        // ===== Search =====
        private void DoSearch_Click(object sender, RoutedEventArgs e)
        {
            var vin = (SearchVin.Text ?? "").Trim().ToLowerInvariant();
            var plate = (SearchPlate.Text ?? "").Trim().ToLowerInvariant();
            var brand = (SearchBrand.Text ?? "").Trim().ToLowerInvariant();
            var model = (SearchModel.Text ?? "").Trim().ToLowerInvariant();

            var q = VehiclesLookup.AsEnumerable();

            if (!string.IsNullOrEmpty(vin)) q = q.Where(v => (v.VIN ?? "").ToLower().Contains(vin));
            if (!string.IsNullOrEmpty(plate)) q = q.Where(v => (v.Plate ?? "").ToLower().Contains(plate));   // FIX: Contains
            if (!string.IsNullOrEmpty(brand)) q = q.Where(v => (v.Brand ?? "").ToLower().Contains(brand));
            if (!string.IsNullOrEmpty(model)) q = q.Where(v => (v.Model ?? "").ToLower().Contains(model));

            SearchResultsGrid.ItemsSource = q.ToList();
        }

        private void ResetSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchVin.Text = "";
            SearchPlate.Text = "";
            SearchBrand.Text = "";
            SearchModel.Text = "";
            SearchResultsGrid.ItemsSource = VehiclesLookup;
        }

        // ===== Helpers =====
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

        private Clients GetOrCreateSalonClient()
        {
            var salon = _db.Clients.FirstOrDefault(c => c.FullName == SalonClientName);
            if (salon != null) return salon;

            salon = new Clients { FullName = SalonClientName, CreatedAt = DateTime.Now };
            _db.Clients.Add(salon);
            _db.SaveChanges();
            return salon;
        }

        // ===== Статусы =====
        private string[] GetAllowedVehicleStatuses()
        {
            try
            {
                const string sql = @"
SELECT cc.definition
FROM sys.check_constraints cc
JOIN sys.objects o ON cc.parent_object_id = o.object_id
WHERE o.name = N'Vehicles' AND cc.name = N'CK_Vehicles_Status';
";
                var def = _db.Database.SqlQuery<string>(sql).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(def))
                    return new[] { "В наличии", "В резерве", "Продано" };

                var list = new System.Collections.Generic.List<string>();
                var upper = def.ToUpperInvariant();
                int inPos = upper.IndexOf("IN (", StringComparison.Ordinal);
                if (inPos >= 0)
                {
                    int end = def.IndexOf(')', inPos + 3);
                    if (end > inPos)
                    {
                        var inside = def.Substring(inPos + 3, end - (inPos + 3));
                        foreach (var part in inside.Split(','))
                        {
                            var s = part.Trim();
                            if (s.StartsWith("N'")) s = s.Substring(2);
                            if (s.StartsWith("'")) s = s.Substring(1);
                            if (s.EndsWith("'")) s = s.Substring(0, s.Length - 1);
                            if (!string.IsNullOrWhiteSpace(s))
                                list.Add(s);
                        }
                    }
                }
                else
                {
                    var tokens = def.Split('\'');
                    for (int i = 1; i < tokens.Length; i += 2)
                    {
                        var s = tokens[i].Trim();
                        if (!string.IsNullOrWhiteSpace(s))
                            list.Add(s);
                    }
                }
                return list.Distinct().ToArray();
            }
            catch
            {
                return new[] { "В наличии", "В резерве", "Продано" };
            }
        }

        private string FindStatus(string containsRus, string fallback)
        {
            if (_allowedStatuses == null || _allowedStatuses.Length == 0)
                return fallback;

            var found = _allowedStatuses.FirstOrDefault(s =>
                s?.IndexOf(containsRus, StringComparison.OrdinalIgnoreCase) >= 0);

            return string.IsNullOrWhiteSpace(found) ? fallback : found;
        }

        private string StatusAvailable => FindStatus("налич", "В наличии");
        private string StatusReserved => FindStatus("резерв", "В резерве");
        private string StatusSold => FindStatus("продан", "Продано");

        // ===== Logout =====
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите выйти?", "Выход",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var login = new LoginWindow();
                login.Show();
                Close();
            }
        }
    }
}