' Managers/IdentifierCapitalizationManager.vb - Classic VB auto-correction style
Imports System
Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports System.IO
Imports SimpleIDE.Models
Imports SimpleIDE.Editors
Imports SimpleIDE.Interfaces

Namespace Managers
    
    ''' <summary>
    ''' Classic VB-style identifier capitalization manager - auto-corrects as you type
    ''' </summary>
    Public Class IdentifierCapitalizationManager
        
        ' ===== Private Fields =====
        
        Private pProjectManager As ProjectManager
        Private pMainWindow As MainWindow
        Private pIdentifierMap As Dictionary(Of String, String) ' lowercase -> canonical case
        Private pIsIndexing As Boolean = False
        
        ' ===== Events (Simplified) =====
        
        Public Event IndexingStarted()
        Public Event IndexingCompleted(vTotalIdentifiers As Integer)
        
        ' ===== Constructor =====
        
        Public Sub New(vProjectManager As ProjectManager, vMainWindow As MainWindow)
            Try
                pProjectManager = vProjectManager
                pMainWindow = vMainWindow
                pIdentifierMap = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                
                ' Subscribe to project events
                If pProjectManager IsNot Nothing Then
                    AddHandler pProjectManager.ProjectLoaded, AddressOf OnProjectOpened
                    AddHandler pProjectManager.ProjectClosed, AddressOf OnProjectClosed
                End If
                
            Catch ex As Exception
                Console.WriteLine($"IdentifierCapitalizationManager constructor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Public Methods =====
        
        ''' <summary>
        ''' Get canonical case for an identifier (returns Nothing if not found)
        ''' </summary>
        Public Function GetCanonicalCase(vIdentifier As String) As String
            Try
                Dim lCanonicalCase As String = Nothing
                If pIdentifierMap.TryGetValue(vIdentifier, lCanonicalCase) Then
                    Return lCanonicalCase
                End If
                Return Nothing
                
            Catch ex As Exception
                Console.WriteLine($"GetCanonicalCase error: {ex.Message}")
                Return Nothing
            End Try
        End Function
        
        ''' <summary>
        ''' Register or update an identifier's canonical case
        ''' </summary>
        Public Sub RegisterIdentifier(vIdentifier As String, vCanonicalCase As String)
            Try
                If String.IsNullOrEmpty(vIdentifier) Then Return
                
                ' Store with case-insensitive key but canonical case value
                pIdentifierMap(vIdentifier) = vCanonicalCase
                
                ' Update all open editors immediately
                UpdateIdentifierInOpenEditors(vCanonicalCase)
                
            Catch ex As Exception
                Console.WriteLine($"RegisterIdentifier error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Index entire project for identifier declarations
        ''' </summary>
        Public Async Function IndexProjectAsync() As Task(Of Boolean)
            Try
                If pIsIndexing Then Return False
                If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then Return False
                
                pIsIndexing = True
                RaiseEvent IndexingStarted()
                
                ' Clear existing data
                pIdentifierMap.Clear()
                
                ' Get all project files
                Dim lProjectInfo As ProjectInfo = pProjectManager.GetProjectInfo(pProjectManager.CurrentProjectPath)
                If lProjectInfo Is Nothing Then Return False
                
                ' Process each file
                Await Task.Run(Sub()
                    For Each lFilePath In lProjectInfo.SourceFiles
                        IndexFileDeclarations(lFilePath)
                    Next
                End Sub)
                
                pIsIndexing = False
                RaiseEvent IndexingCompleted(pIdentifierMap.Count)
                
                Return True
                
            Catch ex As Exception
                Console.WriteLine($"IndexProjectAsync error: {ex.Message}")
                pIsIndexing = False
                Return False
            End Try
        End Function
        
        ''' <summary>
        ''' Attach to editor for real-time auto-correction
        ''' </summary>
        Public Sub AttachToEditor(vEditor As CustomDrawingEditor, vFilePath As String)
            Try
                ' Subscribe to identifier typed events
                AddHandler vEditor.IdentifierTyped, AddressOf OnIdentifierTyped
                AddHandler vEditor.DeclarationDetected, AddressOf OnDeclarationDetected
                
            Catch ex As Exception
                Console.WriteLine($"AttachToEditor error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Private Methods =====
        
        ''' <summary>
        ''' Index declarations in a single file
        ''' </summary>
        Private Sub IndexFileDeclarations(vFilePath As String)
            Try
                If Not File.Exists(vFilePath) Then Return
                
                Dim lContent As String = File.ReadAllText(vFilePath)
                Dim lLines() As String = lContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None)
                
                ' Simple regex patterns for VB.NET declarations
                Dim lPatterns As String() = {
                    "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared)?\s*(?:ReadOnly)?\s*Dim\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared)?\s*Sub\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared)?\s*Function\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*(?:Shared)?\s*Property\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*Class\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*Module\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*Interface\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*Structure\s+(\w+)",
                    "^\s*(?:Public|Private|Protected|Friend)?\s*Enum\s+(\w+)"
                }
                
                For Each lLine In lLines
                    For Each lPattern In lPatterns
                        Dim lMatch As System.Text.RegularExpressions.Match = 
                            System.Text.RegularExpressions.Regex.Match(lLine, lPattern, 
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                        
                        If lMatch.Success Then
                            Dim lIdentifier As String = lMatch.Groups(1).Value
                            ' Store the case as written in the declaration
                            pIdentifierMap(lIdentifier) = lIdentifier
                        End If
                    Next
                Next
                
            Catch ex As Exception
                Console.WriteLine($"IndexFileDeclarations error for {vFilePath}: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handle identifier typed in editor - auto-correct immediately
        ''' </summary>
        Private Sub OnIdentifierTyped(vSender As Object, vArgs As CustomDrawingEditor.IdentifierTypedEventArgs)
            Try
                Dim lEditor As CustomDrawingEditor = TryCast(vSender, CustomDrawingEditor)
                If lEditor Is Nothing Then Return
                
                Dim lCanonicalCase As String = GetCanonicalCase(vArgs.Identifier)
                If lCanonicalCase IsNot Nothing AndAlso lCanonicalCase <> vArgs.Identifier Then
                    ' Auto-correct the identifier
                    lEditor.ReplaceIdentifierAt(vArgs.Line, vArgs.Column, vArgs.Identifier, lCanonicalCase)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnIdentifierTyped error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handle new declaration detected - update canonical case
        ''' </summary>
        Private Sub OnDeclarationDetected(vSender As Object, vArgs As CustomDrawingEditor.DeclarationDetectedEventArgs)
            Try
                ' Register the new identifier with its declared case
                RegisterIdentifier(vArgs.Identifier, vArgs.Identifier)
                
            Catch ex As Exception
                Console.WriteLine($"OnDeclarationDetected error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Update identifier case in all open editors
        ''' </summary>
        Private Sub UpdateIdentifierInOpenEditors(vCanonicalCase As String)
            Try
                For Each lTabEntry As KeyValuePair(Of String, TabInfo) In pMainWindow.OpenTabs()
                    Dim lEditor As CustomDrawingEditor = TryCast(lTabEntry.Value.Editor, CustomDrawingEditor)
                    If lEditor IsNot Nothing Then
                        lEditor.UpdateIdentifierCase(vCanonicalCase)
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"UpdateIdentifierInOpenEditors error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Event Handlers =====
        
        Private Sub OnProjectOpened(vProjectPath As String)
            Try
                ' Auto-index the project
                Task.Run(Async Function()
                    Await IndexProjectAsync()
                End Function)
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectOpened error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnProjectClosed()
            Try
                pIdentifierMap.Clear()
                
            Catch ex As Exception
                Console.WriteLine($"OnProjectClosed error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
