*Files to look at*:

* [DashboardConfig.cs](./CS/AspNetCoreDataFederation/Startup.cs)

# ASP.NET Core Dashboard - How to Register a Federated Data Source

This example registers the [DashboardFederationDataSource](https://docs.devexpress.com/Dashboard/DevExpress.DashboardCommon.DashboardFederationDataSource) from the following set of [data sources](https://docs.devexpress.com/Dashboard/116522):

* [DashboardSqlDataSource](https://docs.devexpress.com/Dashboard/401437) (the SQLite database)
* [DashboardExcelDataSource](https://docs.devexpress.com/Dashboard/401433)
* [DashboardObjectDataSource](https://docs.devexpress.com/Dashboard/401435)
* [DashboardJsonDataSource](https://docs.devexpress.com/Dashboard/401431)

The federated data source is stored in the in-memory storage ([DataSourceInMemoryStorage](https://docs.devexpress.com/Dashboard/DevExpress.DashboardWeb.DataSourceInMemoryStorage)) and is available from the [Add Data Source](https://docs.devexpress.com/Dashboard/117456/web-dashboard/create-dashboards-on-the-web/providing-data/manage-data-sources) dialog. Note that when you add a federated data source to a dashboard, all data sources used in the federated data source are also added to the dashboard.

## Documentation

* [ASP.NET Core Framework - Register a Federated Data Source](https://docs.devexpress.com/Dashboard/402456)
* [Register Default Data Sources](https://docs.devexpress.com/Dashboard/116482)

## Examples

- []()
- []()