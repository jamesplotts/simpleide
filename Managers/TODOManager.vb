' Utilities/TODOManager.vb - TODO file management and code parsing utilities
Imports System
Imports System.IO
Imports System.Collections.Generic
Imports System.Text.RegularExpressions
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Linq
Imports SimpleIDE.Models

Namespace Utilities
    Public Class TODOManager
        
        Private pProjectRoot As String = ""
        Private pTODOFilePath As String = ""
        Private pCodeTODOPattern As New Regex("'[\s]*TODO:[\s]*(.+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
        Private pCodeFIXEDPattern As New Regex("'[\s]*FIXED:[\s]*(.+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
        Private pCodeNOTEPattern As New Regex("'[\s]*NOTE:[\s]*(.+)", RegexOptions.IgnoreCase Or RegexOptions.Compiled)
        
        ' Events
        Public Event TODOsChanged()
        
        Public Sub New(vProjectRoot As String)
            Try
                pProjectRoot = vProjectRoot
                pTODOFilePath = System.IO.Path.Combine(pProjectRoot, "TODO.json")
                
                ' Create TODO file if it doesn't exist
                EnsureTODOFileExists()
                
            Catch ex As Exception
                Console.WriteLine($"error initializing TODOManager: {ex.Message}")
            End Try
        End Sub
        
        Private Sub EnsureTODOFileExists()
            Try
                If Not File.Exists(pTODOFilePath) Then
                    ' Create initial TODO file with sample data
                    Dim                     lSampleTODOs As New List(Of TODOItem) From {
                        New TODOItem("Welcome to TODO Panel") With {
                            .Description = "this is your enhanced TODO management system. You can:" & Environment.NewLine &
                                          "• Add manual tasks with priorities and categories" & Environment.NewLine &
                                          "• Set due dates and track Progress" & Environment.NewLine &
                                          "• View TODO comments from your codebase" & Environment.NewLine &
                                          "• Send tasks to AI Assistant for help" & Environment.NewLine &
                                          "• Filter and search your tasks",
                            .Priority = TODOItem.ePriority.eMedium,
                            .Category = TODOItem.eCategory.eDocumentation,
                            .Status = TODOItem.eStatus.ePending
                        }
                    }
                    
                    SaveTODOs(lSampleTODOs)
                End If
                Console.WriteLine("EnsureTODOFileExists() Done")
            Catch ex As Exception
                Console.WriteLine($"error ensuring TODO file exists: {ex.Message}")
            End Try
        End Sub
        
        Public Function LoadTODOs() As List(Of TODOItem)
            Dim lTODOs As New List(Of TODOItem)
            
            Try
                If File.Exists(pTODOFilePath) Then
                    Dim lJson As String = File.ReadAllText(pTODOFilePath)
                    If Not String.IsNullOrEmpty(lJson) Then
                        Dim lOptions As New JsonSerializerOptions With {
                            .PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            .WriteIndented = True
                        }
                        
                        lTODOs = JsonSerializer.Deserialize(Of List(Of TODOItem))(lJson, lOptions)
                        
                        ' Ensure all items have valid IDs
                        For Each lItem In lTODOs
                            If String.IsNullOrEmpty(lItem.Id) Then
                                lItem.Id = Guid.NewGuid().ToString()
                            End If
                        Next
                    End If
                End If
                
                ' Add code TODOs
                lTODOs.AddRange(ScanCodebaseTODOs())
                
            Catch ex As Exception
                Console.WriteLine($"error loading TODOs: {ex.Message}")
                
                ' Return sample data on error
                lTODOs.Add(New TODOItem("error loading TODOs") With {
                    .Description = $"error: {ex.Message}",
                    .Priority = TODOItem.ePriority.eHigh,
                    .Category = TODOItem.eCategory.eBug
                })
            End Try
            
            Return lTODOs
        End Function
        
        Public Sub SaveTODOs(vTODOs As List(Of TODOItem))
            Try
                ' Only save manual TODOs (not code comments)
                Dim lManualTODOs = vTODOs.Where(Function(t) t.SourceType = TODOItem.eSourceType.eManual).ToList()
                
                Dim lOptions As New JsonSerializerOptions With {
                    .PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    .WriteIndented = True
                }
                
                Dim lJson As String = JsonSerializer.Serialize(lManualTODOs, lOptions)
                File.WriteAllText(pTODOFilePath, lJson)
                
                RaiseEvent TODOsChanged()
                
            Catch ex As Exception
                Console.WriteLine($"error saving TODOs: {ex.Message}")
                Throw
            End Try
        End Sub
        
        Public Function ScanCodebaseTODOs() As List(Of TODOItem)
            Dim lCodeTODOs As New List(Of TODOItem)
            
            Try
                If String.IsNullOrEmpty(pProjectRoot) OrElse Not Directory.Exists(pProjectRoot) Then
                    Return lCodeTODOs
                End If
                
                ' Get all VB.NET files in project
                Dim lVbFiles = Directory.GetFiles(pProjectRoot, "*.vb", SearchOption.AllDirectories)
                
                For Each lFilePath In lVbFiles
                    Try
                        ScanFileForTODOs(lFilePath, lCodeTODOs)
                    Catch ex As Exception
                        Console.WriteLine($"error scanning file {lFilePath}: {ex.Message}")
                    End Try
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error scanning codebase for TODOs: {ex.Message}")
            End Try
            
            Return lCodeTODOs
        End Function
        
        Private Sub ScanFileForTODOs(vFilePath As String, vTODOs As List(Of TODOItem))
            Try
                Dim lLines = File.ReadAllLines(vFilePath)
                
                For lLineIndex As Integer = 0 To lLines.Length - 1
                    Dim lLine As String = lLines(lLineIndex)
                    Dim lLineNumber As Integer = lLineIndex + 1
                    
                    ' Check for TODO comments
                    Dim lTODOMatch As Match = pCodeTODOPattern.Match(lLine)
                    If lTODOMatch.Success Then
                        Dim lTODOText As String = lTODOMatch.Groups(1).Value.Trim()
                        Dim lItem As TODOItem = TODOItem.FromCodeComment(lTODOText, vFilePath, lLineNumber, lTODOMatch.Index, lLine.Trim())
                        lItem.Category = TODOItem.eCategory.eOther
                        lItem.Priority = TODOItem.ePriority.eMedium
                        vTODOs.Add(lItem)
                        Continue For
                    End If
                    
                    ' Check for FIXED comments (mark as completed)
                    Dim lFIXEDMatch As Match = pCodeFIXEDPattern.Match(lLine)
                    If lFIXEDMatch.Success Then
                        Dim lFIXEDText As String = lFIXEDMatch.Groups(1).Value.Trim()
                        Dim lItem As TODOItem = TODOItem.FromCodeComment($"FIXED: {lFIXEDText}", vFilePath, lLineNumber, lFIXEDMatch.Index, lLine.Trim())
                        lItem.Category = TODOItem.eCategory.eOther
                        lItem.Priority = TODOItem.ePriority.eLow
                        lItem.Status = TODOItem.eStatus.eCompleted
                        lItem.CompletedDate = File.GetLastWriteTime(vFilePath)
                        vTODOs.Add(lItem)
                        Continue For
                    End If
                    
                    ' Check for NOTE comments
                    Dim lNOTEMatch As Match = pCodeNOTEPattern.Match(lLine)
                    If lNOTEMatch.Success Then
                        Dim lNOTEText As String = lNOTEMatch.Groups(1).Value.Trim()
                        Dim lItem As TODOItem = TODOItem.FromCodeComment($"NOTE: {lNOTEText}", vFilePath, lLineNumber, lNOTEMatch.Index, lLine.Trim())
                        lItem.Category = TODOItem.eCategory.eDocumentation
                        lItem.Priority = TODOItem.ePriority.eLow
                        vTODOs.Add(lItem)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"error scanning file {vFilePath}: {ex.Message}")
            End Try
        End Sub
        
        Public Function AddTODO(vTitle As String, vDescription As String, vPriority As TODOItem.ePriority, vCategory As TODOItem.eCategory) As TODOItem
            Try
                Dim lItem As New TODOItem(vTitle, vDescription) With {
                    .Priority = vPriority,
                    .Category = vCategory,
                    .SourceType = TODOItem.eSourceType.eManual
                }
                
                ' Load existing TODOs and add new one
                Dim lTODOs = LoadTODOs()
                Dim lManualTODOs = lTODOs.Where(Function(t) t.SourceType = TODOItem.eSourceType.eManual).ToList()
                lManualTODOs.Add(lItem)
                
                SaveTODOs(lManualTODOs)
                
                Return lItem
                
            Catch ex As Exception
                Console.WriteLine($"error adding TODO: {ex.Message}")
                Throw
            End Try
        End Function
        
        Public Sub UpdateTODO(vTODO As TODOItem)
            Try
                If vTODO.SourceType <> TODOItem.eSourceType.eManual Then
                    Throw New InvalidOperationException("Cannot update code comment TODOs")
                End If
                
                ' Load existing TODOs
                Dim lTODOs = LoadTODOs()
                Dim lManualTODOs = lTODOs.Where(Function(t) t.SourceType = TODOItem.eSourceType.eManual).ToList()
                
                ' Find and update the item
                For i As Integer = 0 To lManualTODOs.Count - 1
                    If lManualTODOs(i).Id = vTODO.Id Then
                        lManualTODOs(i) = vTODO
                        Exit For
                    End If
                Next
                
                SaveTODOs(lManualTODOs)
                
            Catch ex As Exception
                Console.WriteLine($"error updating TODO: {ex.Message}")
                Throw
            End Try
        End Sub
        
        Public Sub DeleteTODO(vTODOId As String)
            Try
                ' Load existing TODOs
                Dim lTODOs = LoadTODOs()
                Dim lManualTODOs = lTODOs.Where(Function(t) t.SourceType = TODOItem.eSourceType.eManual).ToList()
                
                ' Remove the item
                lManualTODOs.RemoveAll(Function(t) t.Id = vTODOId)
                
                SaveTODOs(lManualTODOs)
                
            Catch ex As Exception
                Console.WriteLine($"error deleting TODO: {ex.Message}")
                Throw
            End Try
        End Sub
        
        Public Function FilterTODOs(vTODOs As List(Of TODOItem), vFilter As String, vPriorityFilter As TODOItem.ePriority?, vCategoryFilter As TODOItem.eCategory?, vStatusFilter As TODOItem.eStatus?, vShowOverdueOnly As Boolean) As List(Of TODOItem)
            Try
                Dim lFiltered = vTODOs.AsEnumerable()
                
                ' Text filter
                If Not String.IsNullOrEmpty(vFilter) Then
                    lFiltered = lFiltered.Where(Function(t) 
                        Return ( t.Title.IndexOf(vFilter, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                        t.Description.IndexOf(vFilter, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                        t.Tags.any(Function(tag) tag.IndexOf(vFilter, StringComparison.OrdinalIgnoreCase) >= 0) )
                    End Function)
                End If
                
                ' Priority filter
                If vPriorityFilter.HasValue AndAlso vPriorityFilter.Value <> TODOItem.ePriority.eUnspecified Then
                    lFiltered = lFiltered.Where(Function(t) t.Priority = vPriorityFilter.Value)
                End If
                
                ' Category filter
                If vCategoryFilter.HasValue AndAlso vCategoryFilter.Value <> TODOItem.eCategory.eUnspecified Then
                    lFiltered = lFiltered.Where(Function(t) t.Category = vCategoryFilter.Value)
                End If
                
                ' Status filter
                If vStatusFilter.HasValue AndAlso vStatusFilter.Value <> TODOItem.eStatus.eUnspecified Then
                    lFiltered = lFiltered.Where(Function(t) t.Status = vStatusFilter.Value)
                End If
                
                ' Overdue filter
                If vShowOverdueOnly Then
                    lFiltered = lFiltered.Where(Function(t) t.IsOverdue())
                End If
                
                Return lFiltered.ToList()
                
            Catch ex As Exception
                Console.WriteLine($"error filtering TODOs: {ex.Message}")
                Return vTODOs
            End Try
        End Function
        
        Public Function GetTODOStatistics(vTODOs As List(Of TODOItem)) As Dictionary(Of String, Integer)
            Dim lStats As New Dictionary(Of String, Integer)
            
            Try
                lStats("Total") = vTODOs.Count
                lStats("Manual") = vTODOs.Where(Function(t) t.SourceType = TODOItem.eSourceType.eManual).Count()
                lStats("CodeComments") = vTODOs.Where(Function(t) t.SourceType = TODOItem.eSourceType.eCodeComment).Count()
                lStats("Pending") = vTODOs.Where(Function(t) t.Status = TODOItem.eStatus.ePending).Count()
                lStats("InProgress") = vTODOs.Where(Function(t) t.Status = TODOItem.eStatus.eInProgress).Count()
                lStats("Completed") = vTODOs.Where(Function(t) t.Status = TODOItem.eStatus.eCompleted).Count()
                lStats("High") = vTODOs.Where(Function(t) t.Priority = TODOItem.ePriority.eHigh).Count()
                lStats("Critical") = vTODOs.Where(Function(t) t.Priority = TODOItem.ePriority.eCritical).Count()
                lStats("Overdue") = vTODOs.Where(Function(t) t.IsOverdue()).Count()
                lStats("DueSoon") = vTODOs.Where(Function(t) t.IsDueSoon()).Count()
                lStats("Critical") = vTODOs.Where(Function(t) t.Priority = TODOItem.ePriority.eCritical).Count()
                lStats("Overdue") = vTODOs.Where(Function(t) t.IsOverdue()).Count()
                lStats("DueSoon") = vTODOs.Where(Function(t) t.IsDueSoon()).Count()
                 
            Catch ex As Exception
                Console.WriteLine($"error calculating TODO statistics: {ex.Message}")
            End Try
            
            Return lStats
        End Function
        
        ' Property to get TODO file path (for backup/export purposes)
        Public ReadOnly Property TODOFilePath As String
            Get
                Return pTODOFilePath
            End Get
        End Property
        
    End Class
End Namespace
