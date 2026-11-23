using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHostedService<ExpiredBookingCleanup>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

DatabaseHelper.Initialize(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.Use(async (context, next) =>
{
    if (context.Session.GetString("UserType") == "NV")
    {
        object? currentRole = DatabaseHelper.ExecuteScalar(
            "SELECT RoleID FROM NhanVien WHERE MaNV = @MaNV AND TrangThai = 1",
            CommandType.Text, new SqlParameter("@MaNV", context.Session.GetString("UserId")));
        if (currentRole is int role)
        {
            if (context.Session.GetInt32("RoleID") != role)
                context.Session.Clear();
        }
        else
        {
            context.Session.Clear();
        }
    }

    await next(context);
});
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();