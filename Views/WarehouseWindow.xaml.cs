using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using Kursovaya;

namespace Kursovaya.Views
{
    public partial class WarehouseWindow : Window
    {
        private readonly Users _currentUser;
        private readonly user149_dbEntities _db = new user149_dbEntities();

        public WarehouseWindow(Users currentUser)
        {
            _currentUser = currentUser;
            InitializeComponent();
            Loaded += WarehouseWindow_Loaded;
            Closed += (s, e) => _db.Dispose();
        }

        private void WarehouseWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _db.Vehicles.Load();
            _db.Deliveries.Load();
            _db.Suppliers.Load();

            RefreshAvailable_Click(null, null);
            RefreshReserved_Click(null, null);

            NewVehiclesGrid.ItemsSource = _db.Vehicles.Local.Where(v => v.Status == "В наличии").ToList();

            if (WarehouseInfoText != null)
                WarehouseInfoText.Text = $"Пользователь: {_currentUser.Email} ({_currentUser.Role})";
        }

        private void RefreshAvailable_Click(object sender, RoutedEventArgs e)
        {
            var available = _db.Vehicles.Local.Where(v => v.Status == "В наличии").ToList();
            AvailableVehiclesGrid.ItemsSource = available;
            if (AvailableCount != null)
                AvailableCount.Text = available.Count.ToString();
        }

        private void RefreshReserved_Click(object sender, RoutedEventArgs e)
        {
            var reserved = _db.Vehicles.Local.Where(v => v.Status == "В резерве").ToList();
            ReservedVehiclesGrid.ItemsSource = reserved;
            if (ReservedCount != null)
                ReservedCount.Text = reserved.Count.ToString();
        }

        private void AddVehicle_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new AddVehicleSupplyWindow { Owner = this };
            if (wnd.ShowDialog() == true && wnd.NewVehicle != null)
            {
                _db.Vehicles.Load();
                _db.Deliveries.Load();

                NewVehiclesGrid.ItemsSource = _db.Vehicles.Local.Where(v => v.Status == "В наличии").ToList();
                RefreshAvailable_Click(null, null);
                RefreshReserved_Click(null, null);

                MessageBox.Show("Поставка добавлена.", "Склад", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SaveVehicles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _db.SaveChanges();
                RefreshAvailable_Click(null, null);
                RefreshReserved_Click(null, null);
                MessageBox.Show("Изменения сохранены.", "Склад",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Склад",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите выйти?", "Выход",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var loginWindow = new LoginWindow();
                loginWindow.Show();
                this.Close();
            }
        }
    }
}