$subscription = ""
$resourcegroup = ""
$location = "Australia East"
$region = "australiaeast"

# Azure Function & CosmosDB

$funcappstrgaccountname = ""
$functionAppName = ""
$dbname = "TablesDB"
$tablename = "Tutorials"

# Storage Account

$strgaccountname = ""
$sourcepath = "C:\Static App\build"



az login

az account show

az account set --subscription $subscription

az group create --name $resourcegroup --location $location


# Azure Function & CosmosDB

# Create a storage account for the function app. 
az storage account create --name $funcappstrgaccountname --location $location --resource-group $resourcegroup --sku Standard_LRS

# Create a serverless function app in the resource group.
az functionapp create --name $functionAppName --resource-group $resourcegroup --storage-account $funcappstrgaccountname --consumption-plan-location $region --functions-version 3 --disable-app-insights true --os-type Linux --runtime dotnet

# Create an Azure Cosmos DB database using the same function app name.
az cosmosdb create --name $functionAppName --resource-group $resourcegroup --capabilities EnableTable --default-consistency-level BoundedStaleness --enable-public-network true --locations regionName=$location

az cosmosdb database create --name $functionAppName --resource-group $resourcegroup --db-name $dbname

az cosmosdb table create --account-name $functionAppName --resource-group $resourcegroup --name $tablename

$connstring = $(az cosmosdb keys list --name $functionAppName --resource-group $resourcegroup --type connection-strings --query connectionStrings[4].connectionString --output tsv)

# Get the Azure Cosmos DB connection string.
$endpoint=$(az cosmosdb show --name $functionAppName --resource-group $resourcegroup --query documentEndpoint --output tsv)
$key=$(az cosmosdb list-keys --name $functionAppName --resource-group $resourcegroup --query primaryMasterKey --output tsv)



# Configure function app settings to use the Azure Cosmos DB connection string.
az functionapp config appsettings set --name $functionAppName --resource-group $resourcegroup --setting StorageConnectionString=$connstring CosmosDB_Key=$key


# Storage Account

az storage account create -n $strgaccountname -g $resourcegroup --kind StorageV2 -l $region -t Account --sku Standard_LRS

$storagekey = $(az storage account keys list -g $resourcegroup -n $strgaccountname --query [0].value --output tsv)

az storage blob service-properties update --account-name $strgaccountname --static-website --404-document "error.html" --index-document "index.html" --account-key $storagekey

az storage blob upload-batch -s $sourcepath -d '$web' --account-name $strgaccountname --account-key $storagekey

# Find Website url

az storage account show -n $strgaccountname -g $resourcegroup --query "primaryEndpoints.web" --output tsv

