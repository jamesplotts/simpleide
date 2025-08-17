' ScratchpadManager.vb
' Created: 2025-08-05 20:36:28
' ScratchpadManager.vb - Manages scratchpad persistence and operations
Imports System
Imports System.IO
Imports System.Xml.Serialization
Imports System.Collections.Generic
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Managers
    
    Public Class ScratchpadManager
        
        ' Private fields
        Private pSettingsManager As SettingsManager
        Private pGlobalScratchpads As ScratchpadCollection
        Private pProjectScratchpads As ScratchpadCollection
        Private pCurrentProjectPath As String = ""
        Private pGlobalScratchpadPath As String
        Private pProjectScratchpadPath As String
        
        ' Events
        Public Event ScratchpadChanged(vScratchpad As ScratchpadData)
        Public Event ScratchpadListChanged()
        
        ' Constructor
        Public Sub New(vSettingsManager As SettingsManager)
            pSettingsManager = vSettingsManager
            
            ' Initialize global scratchpad path
            pGlobalScratchpadPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimpleIDE",
                "Scratchpads"
            )
            
            ' Ensure directory exists
            If Not Directory.Exists(pGlobalScratchpadPath) Then
                Directory.CreateDirectory(pGlobalScratchpadPath)
            End If
            
            ' Load global scratchpads
            LoadGlobalScratchpads()
        End Sub
        
        ' ===== Project Management =====
        
        Public Sub SetProjectPath(vProjectPath As String)
            Try
                ' Save current project scratchpads if any
                If Not String.IsNullOrEmpty(pCurrentProjectPath) Then
                    SaveProjectScratchpads()
                End If
                
                pCurrentProjectPath = vProjectPath
                
                If Not String.IsNullOrEmpty(vProjectPath) Then
                    ' Set project scratchpad path
                    Dim lProjectDir As String = System.IO.Path.GetDirectoryName(vProjectPath)
                    pProjectScratchpadPath = System.IO.Path.Combine(lProjectDir, ".Scratchpads")
                    
                    ' Ensure directory exists
                    If Not Directory.Exists(pProjectScratchpadPath) Then
                        Directory.CreateDirectory(pProjectScratchpadPath)
                    End If
                    
                    ' Load project scratchpads
                    LoadProjectScratchpads()
                Else
                    pProjectScratchpadPath = ""
                    pProjectScratchpads = New ScratchpadCollection()
                End If
                
                RaiseEvent ScratchpadListChanged()
                
            Catch ex As Exception
                Console.WriteLine($"SetProjectPath error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Get Scratchpads =====
        
        Public Function GetAllScratchpads() As List(Of ScratchpadData)
            Dim lAllScratchpads As New List(Of ScratchpadData)()
            
            ' Add global scratchpads
            If pGlobalScratchpads IsNot Nothing Then
                lAllScratchpads.AddRange(pGlobalScratchpads.Scratchpads)
            End If
            
            ' Add project scratchpads if project is open
            If pProjectScratchpads IsNot Nothing Then
                lAllScratchpads.AddRange(pProjectScratchpads.Scratchpads)
            End If
            
            Return lAllScratchpads
        End Function
        
        Public Function GetScratchpad(vId As String) As ScratchpadData
            ' Check global scratchpads
            If pGlobalScratchpads IsNot Nothing Then
                Dim lGlobal = pGlobalScratchpads.Scratchpads.FirstOrDefault(Function(s) s.Id = vId)
                If lGlobal IsNot Nothing Then Return lGlobal
            End If
            
            ' Check project scratchpads
            If pProjectScratchpads IsNot Nothing Then
                Dim lProject = pProjectScratchpads.Scratchpads.FirstOrDefault(Function(s) s.Id = vId)
                If lProject IsNot Nothing Then Return lProject
            End If
            
            Return Nothing
        End Function
        
        ' ===== Create/Update/Delete =====
        
        Public Function CreateScratchpad(vName As String, vScope As ScratchpadScope) As ScratchpadData
            Try
                Dim lScratchpad As ScratchpadData = Nothing
                
                If vScope = ScratchpadScope.eGlobal Then
                    If pGlobalScratchpads Is Nothing Then
                        pGlobalScratchpads = New ScratchpadCollection()
                    End If
                    lScratchpad = pGlobalScratchpads.AddScratchpad(vName, vScope)
                    SaveGlobalScratchpads()
                    
                ElseIf vScope = ScratchpadScope.eProject AndAlso Not String.IsNullOrEmpty(pCurrentProjectPath) Then
                    If pProjectScratchpads Is Nothing Then
                        pProjectScratchpads = New ScratchpadCollection()
                    End If
                    lScratchpad = pProjectScratchpads.AddScratchpad(vName, vScope)
                    SaveProjectScratchpads()
                End If
                
                RaiseEvent ScratchpadListChanged()
                Return lScratchpad
                
            Catch ex As Exception
                Console.WriteLine($"CreateScratchpad error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        Public Sub UpdateScratchpad(vScratchpad As ScratchpadData)
            Try
                If vScratchpad Is Nothing Then Return
                
                vScratchpad.LastModified = DateTime.Now
                
                ' Save based on scope
                If vScratchpad.Scope = ScratchpadScope.eGlobal Then
                    SaveGlobalScratchpads()
                ElseIf vScratchpad.Scope = ScratchpadScope.eProject Then
                    SaveProjectScratchpads()
                End If
                
                RaiseEvent ScratchpadChanged(vScratchpad)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateScratchpad error: {ex.Message}")
            End Try
        End Sub
        
        Public Sub DeleteScratchpad(vId As String)
            Try
                Dim lScratchpad As ScratchpadData = GetScratchpad(vId)
                If lScratchpad Is Nothing Then Return
                
                If lScratchpad.Scope = ScratchpadScope.eGlobal Then
                    pGlobalScratchpads.RemoveScratchpad(vId)
                    SaveGlobalScratchpads()
                ElseIf lScratchpad.Scope = ScratchpadScope.eProject Then
                    pProjectScratchpads.RemoveScratchpad(vId)
                    SaveProjectScratchpads()
                End If
                
                RaiseEvent ScratchpadListChanged()
                
            Catch ex As Exception
                Console.WriteLine($"DeleteScratchpad error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Load/Save Operations =====
        
        Private Sub LoadGlobalScratchpads()
            Try
                Dim lFilePath As String = System.IO.Path.Combine(pGlobalScratchpadPath, "global_scratchpads.xml")
                
                If File.Exists(lFilePath) Then
                    Dim lSerializer As New XmlSerializer(GetType(ScratchpadCollection))
                    Using lReader As New StreamReader(lFilePath)
                        pGlobalScratchpads = CType(lSerializer.Deserialize(lReader), ScratchpadCollection)
                    End Using
                Else
                    ' Create default global scratchpad
                    pGlobalScratchpads = New ScratchpadCollection()
                    pGlobalScratchpads.AddScratchpad("General Notes", ScratchpadScope.eGlobal)
                    SaveGlobalScratchpads()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadGlobalScratchpads error: {ex.Message}")
                pGlobalScratchpads = New ScratchpadCollection()
            End Try
        End Sub
        
        Private Sub SaveGlobalScratchpads()
            Try
                Dim lFilePath As String = System.IO.Path.Combine(pGlobalScratchpadPath, "global_scratchpads.xml")
                
                Dim lSerializer As New XmlSerializer(GetType(ScratchpadCollection))
                Using lWriter As New StreamWriter(lFilePath)
                    lSerializer.Serialize(lWriter, pGlobalScratchpads)
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"SaveGlobalScratchpads error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub LoadProjectScratchpads()
            Try
                If String.IsNullOrEmpty(pProjectScratchpadPath) Then
                    pProjectScratchpads = New ScratchpadCollection()
                    Return
                End If
                
                Dim lFilePath As String = System.IO.Path.Combine(pProjectScratchpadPath, "project_scratchpads.xml")
                
                If File.Exists(lFilePath) Then
                    Dim lSerializer As New XmlSerializer(GetType(ScratchpadCollection))
                    Using lReader As New StreamReader(lFilePath)
                        pProjectScratchpads = CType(lSerializer.Deserialize(lReader), ScratchpadCollection)
                    End Using
                Else
                    pProjectScratchpads = New ScratchpadCollection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"LoadProjectScratchpads error: {ex.Message}")
                pProjectScratchpads = New ScratchpadCollection()
            End Try
        End Sub
        
        Private Sub SaveProjectScratchpads()
            Try
                If String.IsNullOrEmpty(pProjectScratchpadPath) OrElse pProjectScratchpads Is Nothing Then
                    Return
                End If
                
                Dim lFilePath As String = System.IO.Path.Combine(pProjectScratchpadPath, "project_scratchpads.xml")
                
                Dim lSerializer As New XmlSerializer(GetType(ScratchpadCollection))
                Using lWriter As New StreamWriter(lFilePath)
                    lSerializer.Serialize(lWriter, pProjectScratchpads)
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"SaveProjectScratchpads error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        
        Public Function GetDefaultScratchpad() As ScratchpadData
            ' Return first global scratchpad or create one
            If pGlobalScratchpads IsNot Nothing AndAlso pGlobalScratchpads.Scratchpads.Count > 0 Then
                Return pGlobalScratchpads.Scratchpads(0)
            End If
            
            Return CreateScratchpad("General Notes", ScratchpadScope.eGlobal)
        End Function
        
    End Class
    
End Namespace
