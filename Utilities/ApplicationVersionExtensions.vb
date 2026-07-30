' Utilities/ApplicationVersionExtensions.vb - Extensions for ApplicationVersion
' Adds missing BuildNumber and BuildDate properties
' Created: 2025-08-15

Imports System
Imports System.Reflection
Imports System.IO

Namespace Utilities
    
    ' ===== Extensions for ApplicationVersion =====
    Partial Public Class ApplicationVersion
        
        Private Shared pCachedBuildNumber As String = Nothing
        Private Shared pCachedBuildDate As DateTime? = Nothing
        
        ''' <summary>
        ''' Get the build number (uses the Build component of the version)
        ''' </summary>
        Public Shared ReadOnly Property BuildNumber As String
            Get
                If String.IsNullOrEmpty(pCachedBuildNumber) Then
                    Try
                        Dim lVer As Version = Version
                        pCachedBuildNumber = lVer.Build.ToString()
                        
                        ' If build is 0, use revision as build number
                        If pCachedBuildNumber = "0" AndAlso lVer.Revision > 0 Then
                            pCachedBuildNumber = lVer.Revision.ToString()
                        End If
                        
                    Catch ex As Exception
                        Console.WriteLine($"ApplicationVersion.BuildNumber error: {ex.Message}")
                        pCachedBuildNumber = "0"
                    End Try
                End If
                Return pCachedBuildNumber
            End Get
        End Property
        
        ''' <summary>
        ''' Get the build date (uses assembly file date or current date)
        ''' </summary>
        Public Shared ReadOnly Property BuildDate As DateTime
            Get
                If Not pCachedBuildDate.HasValue Then
                    Try
                        ' Try to get the assembly file date
                        Dim lAssembly As Assembly = Assembly.GetExecutingAssembly()
                        Dim lLocation As String = lAssembly.Location
                        
                        If Not String.IsNullOrEmpty(lLocation) AndAlso File.Exists(lLocation) Then
                            pCachedBuildDate = File.GetLastWriteTime(lLocation)
                        Else
                            ' Fallback to current date
                            pCachedBuildDate = DateTime.Now
                        End If
                        
                    Catch ex As Exception
                        Console.WriteLine($"ApplicationVersion.BuildDate error: {ex.Message}")
                        pCachedBuildDate = DateTime.Now
                    End Try
                End If
                Return pCachedBuildDate.Value
            End Get
        End Property
        
        ''' <summary>
        ''' Clear all cached values including new properties
        ''' </summary>
        Public Shared Sub ClearAllCache()
            ClearCache() ' Call existing method
            pCachedBuildNumber = Nothing
            pCachedBuildDate = Nothing
        End Sub
        
    End Class
    
End Namespace
