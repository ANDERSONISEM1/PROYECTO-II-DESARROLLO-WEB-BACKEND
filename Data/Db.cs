// Api/Data/Db.cs
using System.Data;
using Microsoft.Data.SqlClient;

namespace Api.Data;

public class Db
{
    private readonly string _cs;

    public Db(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("Default") 
                 ?? throw new InvalidOperationException("Falta connection string 'Default'.");

    public IDbConnection Open()
    {
        var cn = new SqlConnection(_cs);
        cn.Open();
        return cn;
    }

    public async Task<IDbConnection> OpenAsync()
    {
        var cn = new SqlConnection(_cs);
        await cn.OpenAsync();
        return cn;
    }
}
