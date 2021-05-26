## Row Level Security

## Getting Started

Prepare database.

```shell
docker-compose up -d
```

create migrations

```shell
cd src/SampleConsole
dotnet ef migrations add InitialCreate
```

apply migration

```shell
dotnet run --project SampleConsole.csproj DML migrate
```

seed

```shell
dotnet run --project SampleConsole.csproj DML seed
```


shutdown database and volume

```shell
docker-compose down --remove-orphans --volumes
```