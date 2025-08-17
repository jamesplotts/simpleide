' Utilities/ApplicationVersion.vb - Centralized application version management
Imports System.Reflection
Imports System.IO

' ApplicationVersion.vb
' Created: 2025-08-10 21:59:54

Namespace Utilities
    
    ''' <summary>
    ''' Centralized application version management
    ''' Gets version from SimpleIDE's own AssemblyInfo
    ''' </summary>
    Public Class ApplicationVersion
        
        Private Shared pCachedVersion As Version = Nothing
        Private Shared pCachedVersionString As String = Nothing
        Private Shared pCachedTitle As String = Nothing
        
        ''' <summary>
        ''' Get the application version from this assembly
        ''' </summary>
        Public Shared ReadOnly Property Version As Version
            Get
                If pCachedVersion Is Nothing Then
                    Try
                        Dim lAssembly As Assembly = Assembly.GetExecutingAssembly()
                        pCachedVersion = lAssembly.GetName().Version
                        
                        If pCachedVersion Is Nothing Then
                            pCachedVersion = New Version(1, 0, 0, 0)
                        End If
                        
                    Catch ex As Exception
                        Console.WriteLine($"ApplicationVersion.Version error: {ex.Message}")
                        pCachedVersion = New Version(1, 0, 0, 0)
                    End Try
                End If
                Return pCachedVersion
            End Get
        End Property
        
        ''' <summary>
        ''' Get the application version as a display string (Major.Minor.Build)
        ''' </summary>
        Public Shared ReadOnly Property VersionString As String
            Get
                If String.IsNullOrEmpty(pCachedVersionString) Then
                    Try
                        Dim lVer As Version = Version
                        pCachedVersionString = $"{lVer.Major}.{lVer.Minor}.{lVer.Build}"
                    Catch ex As Exception
                        Console.WriteLine($"ApplicationVersion.VersionString error: {ex.Message}")
                        pCachedVersionString = "1.0.0"
                    End Try
                End If
                Return pCachedVersionString
            End Get
        End Property
        
        ''' <summary>
        ''' Get the full version string (Major.Minor.Build.Revision)
        ''' </summary>
        Public Shared ReadOnly Property FullVersionString As String
            Get
                Try
                    Dim lVer As Version = Version
                    Return $"{lVer.Major}.{lVer.Minor}.{lVer.Build}.{lVer.Revision}"
                Catch ex As Exception
                    Console.WriteLine($"ApplicationVersion.FullVersionString error: {ex.Message}")
                    Return "1.0.0.0"
                End Try
            End Get
        End Property
        
        ''' <summary>
        ''' Get the application title from AssemblyTitle attribute
        ''' </summary>
        Public Shared ReadOnly Property Title As String
            Get
                If String.IsNullOrEmpty(pCachedTitle) Then
                    Try
                        Dim lAssembly As Assembly = Assembly.GetExecutingAssembly()
                        Dim lTitleAttr As AssemblyTitleAttribute = 
                            DirectCast(Attribute.GetCustomAttribute(lAssembly, GetType(AssemblyTitleAttribute)), AssemblyTitleAttribute)
                        
                        If lTitleAttr IsNot Nothing AndAlso Not String.IsNullOrEmpty(lTitleAttr.Title) Then
                            pCachedTitle = lTitleAttr.Title
                        Else
                            pCachedTitle = "SimpleIDE"
                        End If
                        
                    Catch ex As Exception
                        Console.WriteLine($"ApplicationVersion.Title error: {ex.Message}")
                        pCachedTitle = "SimpleIDE"
                    End Try
                End If
                Return pCachedTitle
            End Get
        End Property
        
        ''' <summary>
        ''' Get copyright information
        ''' </summary>
        Public Shared ReadOnly Property Copyright As String
            Get
                Try
                    Dim lAssembly As Assembly = Assembly.GetExecutingAssembly()
                    Dim lCopyrightAttr As AssemblyCopyrightAttribute = 
                        DirectCast(Attribute.GetCustomAttribute(lAssembly, GetType(AssemblyCopyrightAttribute)), AssemblyCopyrightAttribute)
                    
                    If lCopyrightAttr IsNot Nothing AndAlso Not String.IsNullOrEmpty(lCopyrightAttr.Copyright) Then
                        Return lCopyrightAttr.Copyright
                    End If
                    
                Catch ex As Exception
                    Console.WriteLine($"ApplicationVersion.Copyright error: {ex.Message}")
                End Try
                
                Return $"Copyright © {DateTime.Now.Year}"
            End Get
        End Property
        
        ''' <summary>
        ''' Clear cached values (for testing or reloading)
        ''' </summary>
        Public Shared Sub ClearCache()
            pCachedVersion = Nothing
            pCachedVersionString = Nothing
            pCachedTitle = Nothing
        End Sub
        
    End Class
    
End Namespace
