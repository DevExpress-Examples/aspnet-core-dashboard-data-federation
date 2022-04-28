using DevExpress.AspNetCore;
using DevExpress.DashboardAspNetCore;
using DevExpress.DashboardCommon;
using DevExpress.DashboardWeb;
using DevExpress.DataAccess.DataFederation;
using DevExpress.DataAccess.Excel;
using DevExpress.DataAccess.Json;
using DevExpress.DataAccess.Sql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System;

namespace AspNetCoreDataFederation {
    public class Startup {
        public Startup(IConfiguration configuration, IWebHostEnvironment hostingEnvironment) {
            Configuration = configuration;
            FileProvider = hostingEnvironment.ContentRootFileProvider;
            DashboardExportSettings.CompatibilityMode = DashboardExportCompatibilityMode.Restricted;
        }

        public IFileProvider FileProvider { get; }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            services
                .AddResponseCompression()
                .AddDevExpressControls()
                .AddMvc();

            services.AddScoped<DashboardConfigurator>((IServiceProvider serviceProvider) => {
                DashboardConfigurator configurator = new DashboardConfigurator();
                configurator.SetConnectionStringsProvider(new DashboardConnectionStringsProvider(Configuration));

                DashboardFileStorage dashboardFileStorage = new DashboardFileStorage(FileProvider.GetFileInfo("Data/Dashboards").PhysicalPath);
                configurator.SetDashboardStorage(dashboardFileStorage);

                DataSourceInMemoryStorage dataSourceStorage = new DataSourceInMemoryStorage();

                // Configures an SQL data source.
                DashboardSqlDataSource sqlDataSource = new DashboardSqlDataSource("SQL Data Source", "NWindConnectionString");
                sqlDataSource.DataProcessingMode = DataProcessingMode.Client;
                SelectQuery query = SelectQueryFluentBuilder
                    .AddTable("Orders")
                    .SelectAllColumnsFromTable()
                    .Build("SQL Orders");
                sqlDataSource.Queries.Add(query);

                // Configures an Object data source.
                DashboardObjectDataSource objDataSource = new DashboardObjectDataSource("Object Data Source");
                objDataSource.DataId = "odsInvoices";

                // Configures an Excel data source.
                DashboardExcelDataSource excelDataSource = new DashboardExcelDataSource("Excel Data Source");
                excelDataSource.ConnectionName = "excelSales";
                excelDataSource.FileName = FileProvider.GetFileInfo("Data/SalesPerson.xlsx").PhysicalPath;
                excelDataSource.SourceOptions = new ExcelSourceOptions(new ExcelWorksheetSettings("Data"));

                // Configures a JSON data source.
                DashboardJsonDataSource jsonDataSource = new DashboardJsonDataSource("JSON Data Source");
                jsonDataSource.ConnectionName = "jsonCategories";
                Uri fileUri = new Uri(FileProvider.GetFileInfo("Data/Categories.json").PhysicalPath, UriKind.RelativeOrAbsolute);
                jsonDataSource.JsonSource = new UriJsonSource(fileUri);

                // Registers a Federated data source.
                dataSourceStorage.RegisterDataSource("federatedDataSource", CreateFederatedDataSource(sqlDataSource,
                    excelDataSource, objDataSource, jsonDataSource).SaveToXml());

                configurator.SetDataSourceStorage(dataSourceStorage);

                configurator.DataLoading += (s, e) => {
                    if(e.DataId == "odsInvoices") {
                        e.Data = Invoices.CreateData();
                    }
                };

                configurator.ConfigureDataConnection += (s, e) => {
                    if(e.ConnectionName == "excelSales") {
                        (e.ConnectionParameters as ExcelDataSourceConnectionParameters).FileName = FileProvider.GetFileInfo("Data/SalesPerson.xlsx").PhysicalPath;
                    }
                    else if(e.ConnectionName == "jsonCategories") {
                        UriJsonSource source = new UriJsonSource(new Uri(FileProvider.GetFileInfo("Data/Categories.json").PhysicalPath, UriKind.RelativeOrAbsolute));
                        (e.ConnectionParameters as JsonSourceConnectionParameters).JsonSource = source;
                    }
                };

                return configurator;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            if(env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseDevExpressControls();

            app.UseRouting();
            app.UseEndpoints(endpoints => {
                EndpointRouteBuilderExtension.MapDashboardRoute(endpoints, "api/dashboard", "DefaultDashboard");
                endpoints.MapRazorPages();
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        private static DashboardFederationDataSource CreateFederatedDataSource(DashboardSqlDataSource sqlDS, 
            DashboardExcelDataSource excelDS, DashboardObjectDataSource objDS, DashboardJsonDataSource jsonDS) {

            DashboardFederationDataSource federationDataSource = new DashboardFederationDataSource("Federated Data Source");

            Source sqlSource = new Source("sqlSource", sqlDS, "SQL Orders");
            Source excelSource = new Source("excelSource", excelDS, "");
            Source objectSource = new Source("objectSource", objDS, "");
            SourceNode jsonSourceNode = new SourceNode(new Source("json", jsonDS, ""));

            // Join
            SelectNode joinQuery =
            sqlSource.From()
            .Select("OrderDate", "ShipCity", "ShipCountry")
            .Join(excelSource, "[excelSource.OrderID] = [sqlSource.OrderID]")
                .Select("CategoryName", "ProductName", "Extended Price")
                .Join(objectSource, "[objectSource.Country] = [excelSource.Country]")
                    .Select("Country", "UnitPrice")
                    .Build("Join query");
            federationDataSource.Queries.Add(joinQuery);

            // Union and UnionAll
            UnionNode queryUnionAll = sqlSource.From().Select("OrderID", "OrderDate").Build("OrdersSqlite")
                .UnionAll(excelSource.From().Select("OrderID", "OrderDate").Build("OrdersExcel"))
                .Build("OrdersUnionAll");
            queryUnionAll.Alias = "Union query";

            UnionNode queryUnion = sqlSource.From().Select("OrderID", "OrderDate").Build("OrdersSqlite")
                .Union(excelSource.From().Select("OrderID", "OrderDate").Build("OrdersExcel"))
                .Build("OrdersUnion");
            queryUnion.Alias = "UnionAll query";

            federationDataSource.Queries.Add(queryUnionAll);
            federationDataSource.Queries.Add(queryUnion);

            // Transformation
            TransformationNode unfoldNode = new TransformationNode(jsonSourceNode) {
                Alias = "Unfold",
                Rules = { new TransformationRule { ColumnName = "Products", Alias = "Product", Unfold = true, Flatten = false }}
            };

            TransformationNode unfoldFlattenNode = new TransformationNode(jsonSourceNode) {
                Alias = "Unfold and Flatten",
                Rules = { new TransformationRule { ColumnName = "Products", Unfold = true, Flatten = true }}
            };

            federationDataSource.Queries.Add(unfoldNode);
            federationDataSource.Queries.Add(unfoldFlattenNode);

            return federationDataSource;
        }
    }
}