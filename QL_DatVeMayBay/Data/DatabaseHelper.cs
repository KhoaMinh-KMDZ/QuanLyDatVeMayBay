using Microsoft.Data.SqlClient;
using System.Data;

namespace QL_DatVeMayBay.Data
{
    public static class DatabaseHelper
    {
        private static string _connectionString = string.Empty;

        public static void Initialize(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Chưa cấu hình DefaultConnection trong appsettings.json");
        }

        public static DataTable ExecuteQuery(string query, CommandType cmdType = CommandType.Text, params SqlParameter[] parameters)
        {
            DataTable dt = new DataTable();
            using SqlConnection conn = new SqlConnection(_connectionString);
            using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.CommandType = cmdType;

            if (parameters != null && parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            using SqlDataAdapter adapter = new SqlDataAdapter(cmd);
            adapter.Fill(dt);
            return dt;
        }

        public static int ExecuteNonQuery(string query, CommandType cmdType = CommandType.Text, params SqlParameter[] parameters)
        {
            using SqlConnection conn = new SqlConnection(_connectionString);
            using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.CommandType = cmdType;

            if (parameters != null && parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            conn.Open();
            return cmd.ExecuteNonQuery();
        }

        public static object? ExecuteScalar(string query, CommandType cmdType = CommandType.Text, params SqlParameter[] parameters)
        {
            using SqlConnection conn = new SqlConnection(_connectionString);
            using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.CommandType = cmdType;

            if (parameters != null && parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            conn.Open();
            return cmd.ExecuteScalar();
        }

        /// <summary>
        /// Tạo tham số Table-Valued Parameter cho kiểu ChiTietDatVe_Type trong SQL Server
        /// </summary>
        public static SqlParameter CreateTableParameter(string paramName, string typeName, DataTable tableData)
        {
            return new SqlParameter(paramName, SqlDbType.Structured)
            {
                TypeName = typeName,
                Value = tableData
            };
        }
    }
}