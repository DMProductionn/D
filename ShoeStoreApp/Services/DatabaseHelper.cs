using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Windows;

namespace ShoeStoreApp.Services
{
    internal class DatabaseHelper
    {
        private static string connectionString = ConfigurationManager.ConnectionStrings["ShoeStoreDB"].ConnectionString;

        // Проверка логина
        public static bool CheckLogin(string login, string password, out string fullName, out string role)
        {
            fullName = "";
            role = "";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT FullName, Role FROM Users WHERE Login = @Login AND Password = @Password";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Login", login);
                    cmd.Parameters.AddWithValue("@Password", password);
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        fullName = reader["FullName"].ToString();
                        role = reader["Role"].ToString();
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
                return false;
            }
        }

        public static DataTable GetProducts()
        {
            DataTable table = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = @"SELECT 
                            ProductID, Name, Category, Description, 
                            Brand, Price, PhotoPath, Quantity,
                            'Поставщик' AS Supplier
                         FROM Products";

                SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                adapter.Fill(table);
            }

            return table;
        }

        // Добавление товара
        public static void AddProduct(string article, string name, string category, string description,
                              string brand, string supplier, decimal price, int quantity, string photoPath)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query = @"INSERT INTO Products (Article, Name, Category, Description, Brand, Price, Quantity, PhotoPath) 
                         VALUES (@Article, @Name, @Category, @Description, @Brand, @Price, @Quantity, @PhotoPath)";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Article", article);
                cmd.Parameters.AddWithValue("@Name", name);
                cmd.Parameters.AddWithValue("@Category", category ?? "");
                cmd.Parameters.AddWithValue("@Description", description ?? "");
                cmd.Parameters.AddWithValue("@Brand", brand ?? "");
                cmd.Parameters.AddWithValue("@Price", price);
                cmd.Parameters.AddWithValue("@Quantity", quantity);
                cmd.Parameters.AddWithValue("@PhotoPath", photoPath ?? "");

                cmd.ExecuteNonQuery();
            }
        }

        // Удаление товара
        public static void DeleteProduct(int productId)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string deleteProduct = "DELETE FROM Products WHERE ProductID = @ProductID";
                SqlCommand productCmd = new SqlCommand(deleteProduct, conn);
                productCmd.Parameters.AddWithValue("@ProductID", productId);
                productCmd.ExecuteNonQuery();
            }
        }

    }
}