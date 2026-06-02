using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using ShoeStoreApp.Models;

namespace ShoeStoreApp.Services
{
    internal static class DatabaseHelper
    {
        private static readonly string ConnectionString = GetConnectionString();

        private static readonly object SchemaLock = new object();
        private static bool _schemaEnsured;

        public static bool CheckLogin(string login, string password, out string fullName, out string role)
        {
            fullName = "";
            role = "";

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                const string query =
                    "SELECT FullName, Role FROM Users WHERE Login = @Login AND Password = @Password";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@Login", SqlDbType.NVarChar, 100).Value = login;
                    command.Parameters.Add("@Password", SqlDbType.NVarChar, 100).Value = password;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return false;

                        fullName = reader["FullName"].ToString();
                        role = reader["Role"].ToString();
                        return true;
                    }
                }
            }
        }

        public static DataTable GetProducts()
        {
            EnsureProductSchema();

            DataTable table = new DataTable();

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                const string query = @"
                    SELECT ProductID, Article, Name, Category, Description, Brand,
                           Supplier, Price, UnitOfMeasure, Quantity, Discount, PhotoPath
                    FROM Products";

                using (SqlDataAdapter adapter = new SqlDataAdapter(query, connection))
                {
                    adapter.Fill(table);
                }
            }

            return table;
        }

        public static ProductRecord GetProduct(int productId)
        {
            EnsureProductSchema();

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                const string query = @"
                    SELECT ProductID, Article, Name, Category, Description, Brand,
                           Supplier, Price, UnitOfMeasure, Quantity, Discount, PhotoPath
                    FROM Products
                    WHERE ProductID = @ProductID";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@ProductID", SqlDbType.Int).Value = productId;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            throw new InvalidOperationException("Выбранный товар больше не существует. Обновите список товаров.");

                        return ReadProduct(reader);
                    }
                }
            }
        }

        public static IList<string> GetLookupValues(string columnName)
        {
            EnsureProductSchema();

            HashSet<string> allowedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Category",
                "Brand",
                "Supplier",
                "UnitOfMeasure"
            };

            if (!allowedColumns.Contains(columnName))
                throw new ArgumentException("Запрошен неизвестный справочник товаров.", "columnName");

            List<string> values = new List<string>();

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                string query = string.Format(
                    "SELECT DISTINCT {0} FROM Products WHERE NULLIF(LTRIM(RTRIM({0})), N'') IS NOT NULL ORDER BY {0}",
                    QuoteIdentifier(columnName));

                using (SqlCommand command = new SqlCommand(query, connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        values.Add(reader[0].ToString());
                }
            }

            return values;
        }

        public static int AddProduct(ProductRecord product)
        {
            EnsureProductSchema();

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    bool identityInsertEnabled = false;

                    try
                    {
                        int nextId;

                        using (SqlCommand idCommand = new SqlCommand(
                            "SELECT ISNULL(MAX(ProductID), 0) + 1 FROM Products WITH (UPDLOCK, HOLDLOCK)",
                            connection,
                            transaction))
                        {
                            nextId = Convert.ToInt32(idCommand.ExecuteScalar());
                        }

                        using (SqlCommand identityCommand = new SqlCommand(
                            "SELECT COLUMNPROPERTY(OBJECT_ID(N'Products'), N'ProductID', 'IsIdentity')",
                            connection,
                            transaction))
                        {
                            identityInsertEnabled = Convert.ToInt32(identityCommand.ExecuteScalar()) == 1;
                        }

                        if (identityInsertEnabled)
                            SetIdentityInsert(connection, transaction, true);

                        const string query = @"
                            INSERT INTO Products
                                (ProductID, Article, Name, Category, Description, Brand,
                                 Supplier, Price, UnitOfMeasure, Quantity, Discount, PhotoPath)
                            VALUES
                                (@ProductID, @Article, @Name, @Category, @Description, @Brand,
                                 @Supplier, @Price, @UnitOfMeasure, @Quantity, @Discount, @PhotoPath)";

                        using (SqlCommand command = new SqlCommand(query, connection, transaction))
                        {
                            command.Parameters.Add("@ProductID", SqlDbType.Int).Value = nextId;
                            AddProductParameters(command, product);
                            command.ExecuteNonQuery();
                        }

                        if (identityInsertEnabled)
                            SetIdentityInsert(connection, transaction, false);

                        transaction.Commit();
                        return nextId;
                    }
                    catch
                    {
                        try
                        {
                            if (identityInsertEnabled)
                                SetIdentityInsert(connection, transaction, false);
                        }
                        catch
                        {
                            // The connection is closed immediately below, so session state cannot leak.
                        }

                        try
                        {
                            transaction.Rollback();
                        }
                        catch
                        {
                            // Preserve the original database error if the transaction is already invalid.
                        }

                        throw;
                    }
                }
            }
        }

        public static void UpdateProduct(ProductRecord product)
        {
            EnsureProductSchema();

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                const string query = @"
                    UPDATE Products
                    SET Article = @Article,
                        Name = @Name,
                        Category = @Category,
                        Description = @Description,
                        Brand = @Brand,
                        Supplier = @Supplier,
                        Price = @Price,
                        UnitOfMeasure = @UnitOfMeasure,
                        Quantity = @Quantity,
                        Discount = @Discount,
                        PhotoPath = @PhotoPath
                    WHERE ProductID = @ProductID";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@ProductID", SqlDbType.Int).Value = product.ProductId;
                    AddProductParameters(command, product);

                    if (command.ExecuteNonQuery() == 0)
                        throw new InvalidOperationException("Выбранный товар больше не существует. Обновите список товаров.");
                }
            }
        }

        public static void DeleteProduct(int productId)
        {
            EnsureProductSchema();

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                if (HasDependentRecords(connection, productId))
                {
                    throw new InvalidOperationException(
                        "Нельзя удалить товар: он уже присутствует в заказе. " +
                        "Сохраните товар в каталоге или сначала удалите связанные позиции заказа.");
                }

                try
                {
                    using (SqlCommand command = new SqlCommand(
                        "DELETE FROM Products WHERE ProductID = @ProductID",
                        connection))
                    {
                        command.Parameters.Add("@ProductID", SqlDbType.Int).Value = productId;

                        if (command.ExecuteNonQuery() == 0)
                            throw new InvalidOperationException("Товар уже удалён. Обновите список товаров.");
                    }
                }
                catch (SqlException exception)
                {
                    if (exception.Number == 547)
                    {
                        throw new InvalidOperationException(
                            "Нельзя удалить товар: в базе данных существуют связанные записи, например позиции заказа.",
                            exception);
                    }

                    throw;
                }
            }
        }

        public static bool IsPhotoUsedByAnotherProduct(string photoPath, int productId)
        {
            if (string.IsNullOrWhiteSpace(photoPath))
                return false;

            EnsureProductSchema();

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM Products
                    WHERE PhotoPath = @PhotoPath AND ProductID <> @ProductID",
                    connection))
                {
                    command.Parameters.Add("@PhotoPath", SqlDbType.NVarChar, 500).Value = photoPath;
                    command.Parameters.Add("@ProductID", SqlDbType.Int).Value = productId;
                    return Convert.ToInt32(command.ExecuteScalar()) > 0;
                }
            }
        }

        private static void EnsureProductSchema()
        {
            if (_schemaEnsured)
                return;

            lock (SchemaLock)
            {
                if (_schemaEnsured)
                    return;

                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();

                    // These fields are required by the product form but were absent in the initial database.
                    const string query = @"
                        IF COL_LENGTH(N'Products', N'Supplier') IS NULL
                            ALTER TABLE Products ADD Supplier NVARCHAR(200) NOT NULL
                                CONSTRAINT DF_Products_Supplier DEFAULT(N'');

                        IF COL_LENGTH(N'Products', N'UnitOfMeasure') IS NULL
                            ALTER TABLE Products ADD UnitOfMeasure NVARCHAR(50) NOT NULL
                                CONSTRAINT DF_Products_UnitOfMeasure DEFAULT(N'шт.');

                        IF COL_LENGTH(N'Products', N'Discount') IS NULL
                            ALTER TABLE Products ADD Discount DECIMAL(5, 2) NOT NULL
                                CONSTRAINT DF_Products_Discount DEFAULT(0);";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                _schemaEnsured = true;
            }
        }

        private static bool HasDependentRecords(SqlConnection connection, int productId)
        {
            const string foreignKeysQuery = @"
                SELECT schemas.name AS SchemaName,
                       tables.name AS TableName,
                       columns.name AS ColumnName
                FROM sys.foreign_key_columns AS foreignKeyColumns
                INNER JOIN sys.tables AS tables
                    ON tables.object_id = foreignKeyColumns.parent_object_id
                INNER JOIN sys.schemas AS schemas
                    ON schemas.schema_id = tables.schema_id
                INNER JOIN sys.columns AS columns
                    ON columns.object_id = foreignKeyColumns.parent_object_id
                   AND columns.column_id = foreignKeyColumns.parent_column_id
                WHERE foreignKeyColumns.referenced_object_id = OBJECT_ID(N'Products')";

            if (HasRowsInReferenceTables(connection, foreignKeysQuery, productId))
                return true;

            // Some training databases omit foreign keys. Check conventional order tables as a fallback.
            const string orderTablesQuery = @"
                SELECT schemas.name AS SchemaName,
                       tables.name AS TableName,
                       columns.name AS ColumnName
                FROM sys.tables AS tables
                INNER JOIN sys.schemas AS schemas
                    ON schemas.schema_id = tables.schema_id
                INNER JOIN sys.columns AS columns
                    ON columns.object_id = tables.object_id
                WHERE tables.object_id <> OBJECT_ID(N'Products')
                  AND (LOWER(tables.name) LIKE N'%order%' OR LOWER(tables.name) LIKE N'%заказ%')
                  AND LOWER(columns.name) IN (N'productid', N'product_id', N'идтовара')";

            return HasRowsInReferenceTables(connection, orderTablesQuery, productId);
        }

        private static bool HasRowsInReferenceTables(SqlConnection connection, string metadataQuery, int productId)
        {
            DataTable references = new DataTable();

            using (SqlDataAdapter adapter = new SqlDataAdapter(metadataQuery, connection))
            {
                adapter.Fill(references);
            }

            foreach (DataRow reference in references.Rows)
            {
                string query = string.Format(
                    "SELECT TOP 1 1 FROM {0}.{1} WHERE {2} = @ProductID",
                    QuoteIdentifier(reference["SchemaName"].ToString()),
                    QuoteIdentifier(reference["TableName"].ToString()),
                    QuoteIdentifier(reference["ColumnName"].ToString()));

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.Add("@ProductID", SqlDbType.Int).Value = productId;

                    if (command.ExecuteScalar() != null)
                        return true;
                }
            }

            return false;
        }

        private static ProductRecord ReadProduct(IDataRecord reader)
        {
            return new ProductRecord
            {
                ProductId = Convert.ToInt32(reader["ProductID"]),
                Article = ReadString(reader, "Article"),
                Name = ReadString(reader, "Name"),
                Category = ReadString(reader, "Category"),
                Description = ReadString(reader, "Description"),
                Brand = ReadString(reader, "Brand"),
                Supplier = ReadString(reader, "Supplier"),
                Price = Convert.ToDecimal(reader["Price"]),
                UnitOfMeasure = ReadString(reader, "UnitOfMeasure"),
                Quantity = Convert.ToInt32(reader["Quantity"]),
                Discount = Convert.ToDecimal(reader["Discount"]),
                PhotoPath = ReadString(reader, "PhotoPath")
            };
        }

        private static string ReadString(IDataRecord reader, string columnName)
        {
            return reader[columnName] == DBNull.Value ? "" : reader[columnName].ToString();
        }

        private static void AddProductParameters(SqlCommand command, ProductRecord product)
        {
            command.Parameters.Add("@Article", SqlDbType.NVarChar, 100).Value = product.Article ?? "";
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 250).Value = product.Name ?? "";
            command.Parameters.Add("@Category", SqlDbType.NVarChar, 200).Value = product.Category ?? "";
            command.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value = product.Description ?? "";
            command.Parameters.Add("@Brand", SqlDbType.NVarChar, 200).Value = product.Brand ?? "";
            command.Parameters.Add("@Supplier", SqlDbType.NVarChar, 200).Value = product.Supplier ?? "";

            SqlParameter priceParameter = command.Parameters.Add("@Price", SqlDbType.Decimal);
            priceParameter.Precision = 18;
            priceParameter.Scale = 2;
            priceParameter.Value = product.Price;

            command.Parameters.Add("@UnitOfMeasure", SqlDbType.NVarChar, 50).Value = product.UnitOfMeasure ?? "";
            command.Parameters.Add("@Quantity", SqlDbType.Int).Value = product.Quantity;

            SqlParameter discountParameter = command.Parameters.Add("@Discount", SqlDbType.Decimal);
            discountParameter.Precision = 5;
            discountParameter.Scale = 2;
            discountParameter.Value = product.Discount;

            command.Parameters.Add("@PhotoPath", SqlDbType.NVarChar, 500).Value = product.PhotoPath ?? "";
        }

        private static void SetIdentityInsert(
            SqlConnection connection,
            SqlTransaction transaction,
            bool enabled)
        {
            using (SqlCommand command = new SqlCommand(
                "SET IDENTITY_INSERT Products " + (enabled ? "ON" : "OFF"),
                connection,
                transaction))
            {
                command.ExecuteNonQuery();
            }
        }

        private static string QuoteIdentifier(string value)
        {
            return "[" + value.Replace("]", "]]") + "]";
        }

        private static string GetConnectionString()
        {
            ConnectionStringSettings settings =
                ConfigurationManager.ConnectionStrings["ShoeStoreDB"];

            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new ConfigurationErrorsException(
                    "В App.config не найдена строка подключения ShoeStoreDB. " +
                    "Добавьте обычную строку подключения SQL Server без ключевого слова Provider.");
            }

            return settings.ConnectionString;
        }
    }
}
