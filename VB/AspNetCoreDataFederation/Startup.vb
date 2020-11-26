Imports DevExpress.AspNetCore
Imports DevExpress.DashboardAspNetCore
Imports DevExpress.DashboardCommon
Imports DevExpress.DashboardWeb
Imports DevExpress.DataAccess.DataFederation
Imports DevExpress.DataAccess.Excel
Imports DevExpress.DataAccess.Json
Imports DevExpress.DataAccess.Sql
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.FileProviders
Imports Microsoft.Extensions.Hosting
Imports System

Namespace AspNetCoreDataFederation
	Public Class Startup
		Public Sub New(ByVal configuration As IConfiguration, ByVal hostingEnvironment As IWebHostEnvironment)
			Me.Configuration = configuration
			FileProvider = hostingEnvironment.ContentRootFileProvider
			DashboardExportSettings.CompatibilityMode = DashboardExportCompatibilityMode.Restricted
		End Sub

		Public ReadOnly Property FileProvider() As IFileProvider
		Public ReadOnly Property Configuration() As IConfiguration

		' This method gets called by the runtime. Use this method to add services to the container.
		Public Sub ConfigureServices(ByVal services As IServiceCollection)
			services.AddResponseCompression().AddDevExpressControls().AddMvc().AddDefaultDashboardController(Sub(configurator, serviceProvider)
				configurator.SetConnectionStringsProvider(New DashboardConnectionStringsProvider(Configuration))
				Dim dashboardFileStorage As New DashboardFileStorage(FileProvider.GetFileInfo("Data/Dashboards").PhysicalPath)
				configurator.SetDashboardStorage(dashboardFileStorage)
				Dim dataSourceStorage As New DataSourceInMemoryStorage()
				Dim sqlDataSource As New DashboardSqlDataSource("SQL Data Source", "NWindConnectionString")
				sqlDataSource.DataProcessingMode = DataProcessingMode.Client
				Dim query As SelectQuery = SelectQueryFluentBuilder.AddTable("Orders").SelectAllColumnsFromTable().Build("SQL Orders")
				sqlDataSource.Queries.Add(query)
				Dim objDataSource As New DashboardObjectDataSource("Object Data Source")
				Dim excelDataSource As New DashboardExcelDataSource("Excel Data Source")
				excelDataSource.FileName = FileProvider.GetFileInfo("Data/SalesPerson.xlsx").PhysicalPath
				excelDataSource.SourceOptions = New ExcelSourceOptions(New ExcelWorksheetSettings("Data"))
				Dim jsonDataSource As New DashboardJsonDataSource("JSON Data Source")
				Dim fileUri As New Uri(FileProvider.GetFileInfo("Data/Categories.json").PhysicalPath, UriKind.RelativeOrAbsolute)
				jsonDataSource.JsonSource = New UriJsonSource(fileUri)
				dataSourceStorage.RegisterDataSource("federatedDataSource", CreateFederatedDataSource(sqlDataSource, excelDataSource, objDataSource, jsonDataSource).SaveToXml())
				configurator.SetDataSourceStorage(dataSourceStorage)
				AddHandler configurator.DataLoading, Sub(s, e)
					If e.DataSourceName = "Object Data Source" Then
						e.Data = Invoices.CreateData()
					End If
				End Sub
			End Sub)
		End Sub

		' This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		Public Sub Configure(ByVal app As IApplicationBuilder, ByVal env As IWebHostEnvironment)
			If env.IsDevelopment() Then
				app.UseDeveloperExceptionPage()
			Else
				app.UseExceptionHandler("/Home/Error")
				' The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
				app.UseHsts()
			End If
			app.UseHttpsRedirection()
			app.UseStaticFiles()
			app.UseDevExpressControls()

			app.UseRouting()
			app.UseEndpoints(Sub(endpoints)
				EndpointRouteBuilderExtension.MapDashboardRoute(endpoints, "dashboardControl")
				endpoints.MapRazorPages()
				endpoints.MapControllerRoute(name:= "default", pattern:= "{controller=Home}/{action=Index}/{id?}")
			End Sub)
		End Sub

		Private Shared Function CreateFederatedDataSource(ByVal sqlDS As DashboardSqlDataSource, ByVal excelDS As DashboardExcelDataSource, ByVal objDS As DashboardObjectDataSource, ByVal jsonDS As DashboardJsonDataSource) As DashboardFederationDataSource

			Dim federationDataSource As New DashboardFederationDataSource("Federated Data Source")

			Dim sqlSource As New Source("sqlSource", sqlDS, "SQL Orders")
			Dim excelSource As New Source("excelSource", excelDS, "")
			Dim objectSource As New Source("objectSource", objDS, "")
			Dim jsonSourceNode As New SourceNode(New Source("json", jsonDS, ""))

			' Join
			Dim joinQuery As SelectNode = sqlSource.From().Select("OrderDate", "ShipCity", "ShipCountry").Join(excelSource, "[excelSource.OrderID] = [sqlSource.OrderID]").Select("CategoryName", "ProductName", "Extended Price").Join(objectSource, "[objectSource.Country] = [excelSource.Country]").Select("Country", "UnitPrice").Build("Join query")
			federationDataSource.Queries.Add(joinQuery)

			' Union and UnionAll
			Dim queryUnionAll As UnionNode = sqlSource.From().Select("OrderID", "OrderDate").Build("OrdersSqlite").UnionAll(excelSource.From().Select("OrderID", "OrderDate").Build("OrdersExcel")).Build("OrdersUnionAll")
			queryUnionAll.Alias = "Union query"

			Dim queryUnion As UnionNode = sqlSource.From().Select("OrderID", "OrderDate").Build("OrdersSqlite").Union(excelSource.From().Select("OrderID", "OrderDate").Build("OrdersExcel")).Build("OrdersUnion")
			queryUnion.Alias = "UnionAll query"

			federationDataSource.Queries.Add(queryUnionAll)
			federationDataSource.Queries.Add(queryUnion)

			' Transformation
			Dim unfoldNode As New TransformationNode(jsonSourceNode) With {
				.Alias = "Unfold",
				.Rules = {
					New TransformationRule With {
						.ColumnName = "Products",
						.Alias = "Product",
						.Unfold = True,
						.Flatten = False
					}
				}
			}

			Dim unfoldFlattenNode As New TransformationNode(jsonSourceNode) With {
				.Alias = "Unfold and Flatten",
				.Rules = {
					New TransformationRule With {
						.ColumnName = "Products",
						.Unfold = True,
						.Flatten = True
					}
				}
			}

			federationDataSource.Queries.Add(unfoldNode)
			federationDataSource.Queries.Add(unfoldFlattenNode)

			Return federationDataSource
		End Function
	End Class
End Namespace