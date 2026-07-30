' AssemblyBrowser.vb - Assembly browsing functionality for reference management
Imports System.IO
Imports System.Reflection
Imports System.Collections.Generic
Imports System.Runtime.InteropServices
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Namespace Utilities
    Public Class AssemblyBrowser
        
        ' Assembly info class
        Public Class AssemblyInfo
            Public Property Name As String
            Public Property FullName As String
            Public Property Version As String
            Public Property Runtime As String
            Public Property Location As String
            Public Property IsGAC As Boolean
            
            Public Overrides Function ToString() As String
                If Not String.IsNullOrEmpty(Version) Then
                    Return $"{Name} ({Version})"
                Else
                    Return Name
                End If
            End Function
        End Class
        
        ' Get .NET runtime assemblies
        Public Shared Function GetRuntimeAssemblies() As List(Of AssemblyInfo)
            Dim lAssemblies As New List(Of AssemblyInfo)
            
            Try
                ' Get runtime directory
                Dim lRuntimeDir As String = RuntimeEnvironment.GetRuntimeDirectory()
                Console.WriteLine($"Runtime directory: {lRuntimeDir}")
                
                ' Common runtime assemblies
                Dim lCommonAssemblies As String() = {
                    "System",
                    "System.Core",
                    "System.Data",
                    "System.Drawing",
                    "System.Net.Http",
                    "System.Runtime",
                    "System.Collections",
                    "System.Linq",
                    "System.Text.RegularExpressions",
                    "System.IO",
                    "System.Threading",
                    "System.Threading.Tasks",
                    "System.Xml",
                    "System.Xml.Linq",
                    "Microsoft.VisualBasic",
                    "Microsoft.VisualBasic.Core",
                    "mscorlib"
                }
                
                ' Try to load common assemblies
                for each lAssemblyName in lCommonAssemblies
                    Try
                        Dim lAssembly As Assembly = Assembly.Load(lAssemblyName)
                        Dim lInfo As New AssemblyInfo()
                        lInfo.Name = lAssembly.GetName().Name
                        lInfo.FullName = lAssembly.FullName
                        lInfo.Version = lAssembly.GetName().Version.ToString()
                        lInfo.Runtime = "NET Core"
                        lInfo.Location = If(String.IsNullOrEmpty(lAssembly.Location), "", lAssembly.Location)
                        lInfo.IsGAC = False ' .NET Core doesn't use GAC
                        
                        lAssemblies.Add(lInfo)
                    Catch
                        ' Assembly not available
                    End Try
                Next
                
                ' Also check for assemblies in the runtime directory
                If Directory.Exists(lRuntimeDir) Then
                    Dim lFiles() As String = Directory.GetFiles(lRuntimeDir, "*.dll")
                    for each lFile in lFiles
                        Try
                            Dim lAssemblyName As AssemblyName = AssemblyName.GetAssemblyName(lFile)
                            
                            ' Skip if already added
                            If lAssemblies.any(Function(a) a.Name = lAssemblyName.Name) Then
                                Continue for
                            End If
                            
                            Dim lInfo As New AssemblyInfo()
                            lInfo.Name = lAssemblyName.Name
                            lInfo.FullName = lAssemblyName.FullName
                            lInfo.Version = lAssemblyName.Version.ToString()
                            lInfo.Runtime = "NET Core"
                            lInfo.Location = lFile
                            lInfo.IsGAC = False
                            
                            lAssemblies.Add(lInfo)
                        Catch
                            ' Not a valid assembly
                        End Try
                    Next
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error getting Runtime assemblies: {ex.Message}")
            End Try
            
            ' Sort by name
            lAssemblies.Sort(Function(a, b) String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase))
            
            Return lAssemblies
        End Function
        
        ' Get assemblies from a specific directory
        Public Shared Function GetAssembliesFromDirectory(vDirectory As String) As List(Of AssemblyInfo)
            Dim lAssemblies As New List(Of AssemblyInfo)
            
            Try
                If Not Directory.Exists(vDirectory) Then
                    Return lAssemblies
                End If
                
                Dim lFiles() As String = Directory.GetFiles(vDirectory, "*.dll")
                for each lFile in lFiles
                    Try
                        Dim lAssemblyName As AssemblyName = AssemblyName.GetAssemblyName(lFile)
                        
                        Dim lInfo As New AssemblyInfo()
                        lInfo.Name = lAssemblyName.Name
                        lInfo.FullName = lAssemblyName.FullName
                        lInfo.Version = lAssemblyName.Version.ToString()
                        lInfo.Runtime = "Custom"
                        lInfo.Location = lFile
                        lInfo.IsGAC = False
                        
                        lAssemblies.Add(lInfo)
                    Catch
                        ' Not a valid assembly
                    End Try
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error getting assemblies from directory: {ex.Message}")
            End Try
            
            Return lAssemblies
        End Function
        
        ' Get recently used assemblies (from settings)
        Public Shared Function GetRecentAssemblies(vSettingsManager As SettingsManager) As List(Of String)
            Dim lRecent As New List(Of String)
            
            Try
                Dim lRecentString As String = vSettingsManager.GetSetting("RecentAssemblies", "")
                If Not String.IsNullOrEmpty(lRecentString) Then
                    lRecent.AddRange(lRecentString.Split(";"c))
                End If
            Catch ex As Exception
                Console.WriteLine($"error getting recent assemblies: {ex.Message}")
            End Try
            
            Return lRecent
        End Function
        
        ' Add to recent assemblies
        Public Shared Sub AddToRecentAssemblies(vSettingsManager As SettingsManager, vAssemblyPath As String)
            Try
                Dim lRecent As List(Of String) = GetRecentAssemblies(vSettingsManager)
                
                ' Remove if already exists
                lRecent.Remove(vAssemblyPath)
                
                ' Add to beginning
                lRecent.Insert(0, vAssemblyPath)
                
                ' Keep only last 10
                If lRecent.Count > 10 Then
                    lRecent.RemoveRange(10, lRecent.Count - 10)
                End If
                
                ' Save
                vSettingsManager.SetSetting("RecentAssemblies", String.Join(";", lRecent))
                vSettingsManager.SaveSettings()
                
            Catch ex As Exception
                Console.WriteLine($"error adding to recent assemblies: {ex.Message}")
            End Try
        End Sub
    End Class
End Namespace