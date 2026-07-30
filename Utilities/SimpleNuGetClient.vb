' SimpleNuGetClient.vb - Simplified NuGet client without external JSON dependencies
Imports System.Net.Http
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Text.Json
Imports System.Linq
Imports SimpleIDE.Managers

Namespace Utilities
    Public Class NuGetClient
        
        ' NuGet package info class
        Public Class PackageInfo
            Public Property Id As String
            Public Property Version As String
            Public Property Description As String
            Public Property Authors As List(Of String)
            Public Property ProjectUrl As String
            Public Property LicenseUrl As String
            Public Property TotalDownloads As Long
            Public Property Versions As List(Of String)
            Public Property IsInstalled As Boolean = False
            Public Property InstalledVersion As String
        End Class
        
        ' Search result class
        Public Class SearchResult
            Public Property TotalHits As Integer
            Public Property Packages As List(Of PackageInfo)
            
            Public Sub New()
                Packages = New List(Of PackageInfo)
            End Sub
        End Class
        
        ' Private fields
        Private pHttpClient As HttpClient
        Private pServiceIndexUrl As String = "https://api.nuget.org/v3/index.json"
        Private pSearchUrl As String = "https://azuresearch-usnc.nuget.org/query"
        Private pVersionsUrl As String = ""
        Private pPackageBaseUrl As String = "https://api.nuget.org/v3-flatcontainer/"
        Private pCache As New Dictionary(Of String, SearchResult)
        Private pCacheTimeout As TimeSpan = TimeSpan.FromMinutes(5)
        Private pLastCacheTime As DateTime = DateTime.MinValue
        
        Public Sub New()
            pHttpClient = New HttpClient()
            pHttpClient.DefaultRequestHeaders.Add("User-Agent", "SimpleIDE/1.0")
            
            ' Initialize service URLs
            Task.Run(AddressOf InitializeServiceUrlsAsync).Wait()
        End Sub
        
        ' Initialize NuGet service URLs
        Private Async Function InitializeServiceUrlsAsync() As Task
            Try
                Dim lResponse As String = Await pHttpClient.GetStringAsync(pServiceIndexUrl)
                
                ' Parse JSON using System.Text.Json
                Using lDoc As JsonDocument = JsonDocument.Parse(lResponse)
                    Dim lResources As JsonElement = lDoc.RootElement.GetProperty("resources")
                    
                    For Each lResource In lResources.EnumerateArray()
                        Dim lTypeElement As JsonElement = Nothing
                        Dim lIdElement As JsonElement = Nothing
                        
                        If lResource.TryGetProperty("@Type", lTypeElement) AndAlso lResource.TryGetProperty("@Id", lIdElement) Then
                            Dim lType As String = lTypeElement.GetString()
                            Dim lId As String = lIdElement.GetString()
                            
                            If lType.Contains("SearchQueryService") Then
                                pSearchUrl = lId
                            ElseIf lType.Contains("PackageVersions") Then
                                pVersionsUrl = lId
                            ElseIf lType.Contains("PackageBaseAddress") Then
                                pPackageBaseUrl = lId
                            End If
                        End If
                    Next
                End Using
                
                Console.WriteLine($"NuGet Search Url: {pSearchUrl}")
                
            Catch ex As Exception
                Console.WriteLine($"error initializing NuGet service URLs: {ex.Message}")
                ' Use default URLs as fallback
            End Try
        End Function
        
        ' Search for packages
        Public Async Function SearchPackagesAsync(vQuery As String, vSkip As Integer, vTake As Integer) As Task(Of SearchResult)
            Try
                ' Check cache first
                Dim lCacheKey As String = $"{vQuery}_{vSkip}_{vTake}"
                If pCache.ContainsKey(lCacheKey) AndAlso (DateTime.Now - pLastCacheTime) < pCacheTimeout Then
                    Return pCache(lCacheKey)
                End If
                
                ' Build search URL
                Dim lUrl As String = $"{pSearchUrl}?q={Uri.EscapeDataString(vQuery)}&skip={vSkip}&take={vTake}"
                
                ' Make request
                Dim lResponse As String = Await pHttpClient.GetStringAsync(lUrl)
                
                ' Parse JSON
                Dim lResult As New SearchResult()
                
                Using lDoc As JsonDocument = JsonDocument.Parse(lResponse)
                    Dim lRoot As JsonElement = lDoc.RootElement
                    
                    ' Get total hits
                    Dim lTotalHitsElement As JsonElement = Nothing
                    If lRoot.TryGetProperty("TotalHits", lTotalHitsElement) Then
                        lResult.TotalHits = lTotalHitsElement.GetInt32()
                    End If
                    
                    ' Parse packages
                    Dim lData As JsonElement = Nothing
                    If lRoot.TryGetProperty("Data", lData) Then
                        For Each lPackageData In lData.EnumerateArray()
                            Dim lPackage As New PackageInfo()
                            
                            ' Parse basic properties
                            Dim lElement As JsonElement = Nothing
                            If lPackageData.TryGetProperty("Id", lElement) Then lPackage.Id = lElement.GetString()
                            If lPackageData.TryGetProperty("Version", lElement) Then lPackage.Version = lElement.GetString()
                            If lPackageData.TryGetProperty("Description", lElement) Then lPackage.Description = lElement.GetString()
                            If lPackageData.TryGetProperty("ProjectUrl", lElement) Then lPackage.ProjectUrl = lElement.GetString()
                            If lPackageData.TryGetProperty("LicenseUrl", lElement) Then lPackage.LicenseUrl = lElement.GetString()
                            
                            ' Parse downloads
                            If lPackageData.TryGetProperty("TotalDownloads", lElement) Then
                                lPackage.TotalDownloads = lElement.GetInt64()
                            End If
                            
                            ' Parse authors
                            lPackage.Authors = New List(Of String)()
                            If lPackageData.TryGetProperty("Authors", lElement) Then
                                For Each lAuthor In lElement.EnumerateArray()
                                    lPackage.Authors.Add(lAuthor.GetString())
                                Next
                            End If
                            
                            ' Parse versions
                            lPackage.Versions = New List(Of String)()
                            If lPackageData.TryGetProperty("Versions", lElement) Then
                                For Each lVersion In lElement.EnumerateArray()
                                    Dim lVersionElement As JsonElement = Nothing
                                    If lVersion.TryGetProperty("Version", lVersionElement) Then
                                        lPackage.Versions.Add(lVersionElement.GetString())
                                    End If
                                Next
                            End If
                            
                            lResult.Packages.Add(lPackage)
                        Next
                    End If
                End Using
                
                ' Update cache
                pCache(lCacheKey) = lResult
                pLastCacheTime = DateTime.Now
                
                Return lResult
                
            Catch ex As Exception
                Console.WriteLine($"error searching NuGet Packages: {ex.Message}")
                Return New SearchResult()
            End Try
        End Function
        
        ' Get all versions of a package
        Public Async Function GetPackageVersionsAsync(vPackageId As String) As Task(Of List(Of String))
            Try
                ' Build URL
                Dim lUrl As String = $"{pPackageBaseUrl}{vPackageId.ToLower()}/index.json"
                
                ' Make request
                Dim lResponse As String = Await pHttpClient.GetStringAsync(lUrl)
                
                Dim lVersions As New List(Of String)()
                
                ' Parse JSON
                Using lDoc As JsonDocument = JsonDocument.Parse(lResponse)
                    Dim lVersionsElement As JsonElement
                    If lDoc.RootElement.TryGetProperty("Versions", lVersionsElement) Then
                        For Each lVersion In lVersionsElement.EnumerateArray()
                            lVersions.Add(lVersion.GetString())
                        Next
                    End If
                End Using
                
                ' Sort versions (newest first)
                lVersions.Sort(Function(a, b) CompareVersions(b, a))
                
                Return lVersions
                
            Catch ex As Exception
                Console.WriteLine($"error getting Package Versions: {ex.Message}")
                Return New List(Of String)()
            End Try
        End Function
        
        ' Check if package is installed in project
        Public Function IsPackageInstalled(vProjectFile As String, vPackageId As String, ByRef vInstalledVersion As String) As Boolean
            Try
                Dim lManager As New ReferenceManager()
                Dim lReferences As List(Of ReferenceManager.ReferenceInfo) = lManager.GetAllReferences(vProjectFile)
                
                For Each lRef In lReferences
                    If lRef.Type = ReferenceManager.ReferenceType.ePackage AndAlso
                       lRef.Name.Equals(vPackageId, StringComparison.OrdinalIgnoreCase) Then
                        vInstalledVersion = lRef.Version
                        Return True
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error checking if Package is installed: {ex.Message}")
            End Try
            
            Return False
        End Function
        
        ' Compare version strings
        Private Function CompareVersions(vVersion1 As String, vVersion2 As String) As Integer
            Try
                ' Simple version comparison
                Dim lParts1() As String = vVersion1.Split("."c)
                Dim lParts2() As String = vVersion2.Split("."c)
                
                For i As Integer = 0 To Math.Min(lParts1.Length - 1, lParts2.Length - 1)
                    Dim lNum1 As Integer = 0
                    Dim lNum2 As Integer = 0
                    
                    ' Try to parse as integers
                    Integer.TryParse(lParts1(i), lNum1)
                    Integer.TryParse(lParts2(i), lNum2)
                    
                    If lNum1 <> lNum2 Then
                        Return lNum1.CompareTo(lNum2)
                    End If
                Next
                
                ' If all compared parts are equal, longer version is greater
                Return lParts1.Length.CompareTo(lParts2.Length)
                
            Catch ex As Exception
                ' Fallback to string comparison
                Return String.Compare(vVersion1, vVersion2)
            End Try
        End Function
    End Class
End Namespace
