// Learn more about F# at http://fsharp.org

open System

open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Auth
open Microsoft.WindowsAzure.Storage.Blob
open System.Collections.Generic
open System.IO
open System.Net.Http
open System.Net.Http.Headers

type AzureBlobDataReference = {
    ConnectionString: string
    RelativeLocation: string
    //BaseLocation: string
    //SasBlobToken: string
}

type BatchScoreStatusCode = 
    | NotStarted = 0
    | Running = 1
    | Failed = 2
    | Cancelled = 3
    | Finished = 4

type BatchScoreStatus = {
    StatusCode: BatchScoreStatusCode
    Restuls:IDictionary<string, AzureBlobDataReference> 
}

type BatchExecutionRequest = {
    Inputs: IDictionary<string, AzureBlobDataReference>
    GlobalParameters: IDictionary<string,string>
    Outputs: IDictionary<string,AzureBlobDataReference>
}

let outputFileLocation = @"Data/MyResult.csv"
let localFile = @"Data/Data.csv"
let storageAccountName = "wksa1"
let baseUrl = "https://ussouthcentral.services.azureml.net/workspaces/b520db679c374a07a5335fdd1c879feb/services/a356d3cedb2243f4b05b08e51c3ba80c/jobs"
let storageAccountKey = Environment.GetEnvironmentVariable("az_storeage_key")
let storageContainerName = "blob1"
let apiKey = Environment.GetEnvironmentVariable("az_titanic_key")

let uploadFileToBlob localFile targetFile containerName connectionString = 
    if File.Exists localFile |> not then 
        raise <| FileNotFoundException("File doesn't exist on local computer")

    printfn "Uploading the input to blob storage..."

    let client = CloudStorageAccount.Parse(connectionString).CreateCloudBlobClient()
    let container = client.GetContainerReference(containerName)

    async {
        do! (container.CreateIfNotExistsAsync()  |> Async.AwaitTask |> Async.Ignore)
        let blob = container.GetBlockBlobReference(targetFile) 
        return blob.UploadFromFileAsync localFile |> Async.AwaitTask
    }
    |> Async.RunSynchronously
    |> Async.Ignore

let writeFailedResponse (response: HttpResponseMessage) = 
    printfn "The request failed with status code: %A" response.StatusCode
    printfn "%s" <| response.Headers.ToString()

    response.Content.ReadAsAsync() 
    |> Async.AwaitTask 
    |> Async.RunSynchronously
    |> printfn "%s"

let invokeBatchExecution() = 
    let storageConnectionString = sprintf "DefaultEndpointsProtocal=https;AccountName%s;AccountKey=%s" storageAccountName storageAccountKey

    uploadFileToBlob localFile "input5datablob.csv" storageContainerName storageConnectionString |> ignore

    use client = new HttpClient()
    let request = { 
        GlobalParameters = [] |> dict
        Inputs = 
            [ 
                ( "input1", { ConnectionString = storageConnectionString; RelativeLocation = sprintf "%s/input5datablob.csv" storageContainerName }) 
            ] |> dict
        Outputs =
            [ 
                ( "output1", { ConnectionString = storageConnectionString; RelativeLocation = sprintf "%s/output5result.csv" storageContainerName })
            ] |> dict
    }

    client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", apiKey)
    
    printfn "Submitting the job..." 

    let response = 
        client.PostAsJsonAsync (baseUrl + "?api-version=2.0", request) 
        |> Async.AwaitTask
        |> Async.RunSynchronously

    match response.IsSuccessStatusCode with
    | false ->
        response |> writeFailedResponse
    | true ->
        let jobId =
            response.Content.ReadAsAsync<string>() |> Async.AwaitTask |> Async.RunSynchronously
        () // TODO

[<EntryPoint>]
let main argv =
    printfn "Hello World from F#!"
    0 // return an integer exit code
