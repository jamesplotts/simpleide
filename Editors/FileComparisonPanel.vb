' Refactored FileComparisonPanel.vb - Updated to use SourceFileInfo architecture
Imports System
Imports System.IO
Imports System.Text
Imports System.Collections.Generic
Imports Gtk
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Namespace Editors
    
    ''' <summary>
    ''' Panel for comparing two files side-by-side with synchronized scrolling
    ''' </summary>
    Public Class FileComparisonPanel
        Inherits Box
        
        ' ===== Private Fields =====
        Private pMainPaned As Paned
        Private pLeftContainer As Box
        Private pRightContainer As Box
        Private pLeftEditor As CustomDrawingEditor
        Private pRightEditor As CustomDrawingEditor
        Private pLeftHeader As Box
        Private pRightHeader As Box
        Private pLeftFileLabel As Label
        Private pRightFileLabel As Label
        Private pToolbar As Toolbar
        Private pSyncScrollButton As ToggleToolButton
        Private pShowDifferencesButton As ToggleToolButton
        Private pSwapButton As ToolButton
        Private pNextDiffButton As ToolButton
        Private pPrevDiffButton As ToolButton
        
        ' SourceFileInfo for each editor
        Private pLeftSourceFileInfo As SourceFileInfo
        Private pRightSourceFileInfo As SourceFileInfo
        
        ' Dependencies
        Private pSyntaxColorSet As SyntaxColorSet
        Private pSettingsManager As SettingsManager
        Private pProjectManager As ProjectManager
        
        ' State
        Private pSyncScrolling As Boolean = True
        Private pShowDifferences As Boolean = True
        Private pDifferences As List(Of DifferenceInfo)
        Private pCurrentDifferenceIndex As Integer = -1
        Private pIsUpdatingScroll As Boolean = False
        
        ' ===== Difference Info Structure =====
        Public Structure DifferenceInfo
            Public LeftStartLine As Integer
            Public LeftEndLine As Integer
            Public RightStartLine As Integer
            Public RightEndLine As Integer
            Public Type As DifferenceType
        End Structure
        
        Public Enum DifferenceType
            eAdded
            eDeleted
            eModified
        End Enum

        ' ===== Events =====
        Public Event FilesSwapped()
        Public Event DifferenceNavigated(vDifferenceIndex As Integer, vTotalDifferences As Integer)

        
        ' ===== Constructor =====
        Public Sub New(vSyntaxColorSet As SyntaxColorSet, vSettingsManager As SettingsManager, Optional vProjectManager As ProjectManager = Nothing)
            MyBase.New(Orientation.Vertical, 0)
            
            Try
                pSyntaxColorSet = vSyntaxColorSet
                pSettingsManager = vSettingsManager
                pProjectManager = vProjectManager
                pDifferences = New List(Of DifferenceInfo)()
                InitializeComponents()
                ApplyStyling()
            Catch ex As Exception
                Console.WriteLine($"FileComparisonPanel.New error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Updated Initialization to create editors with SourceFileInfo =====
        Private Sub InitializeComponents()
            Try
                ' Create toolbar
                CreateToolbar()
                
                ' Create main paned container
                pMainPaned = New Paned(Orientation.Horizontal)
                
                ' Create left container
                pLeftContainer = New Box(Orientation.Vertical, 0)
                CreateLeftHeader()
                
                ' Create left editor with temporary SourceFileInfo
                pLeftSourceFileInfo = New SourceFileInfo("", "")
                pLeftSourceFileInfo.TextLines.Add("")
                pLeftEditor = New CustomDrawingEditor(pLeftSourceFileInfo)
                pLeftEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                pLeftContainer.PackStart(pLeftHeader, False, False, 0)
                pLeftContainer.PackStart(pLeftEditor, True, True, 0)
                
                ' Create right container
                pRightContainer = New Box(Orientation.Vertical, 0)
                CreateRightHeader()
                
                ' Create right editor with temporary SourceFileInfo
                pRightSourceFileInfo = New SourceFileInfo("", "")
                pRightSourceFileInfo.TextLines.Add("")
                pRightEditor = New CustomDrawingEditor(pRightSourceFileInfo)
                pRightEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                pRightContainer.PackStart(pRightHeader, False, False, 0)
                pRightContainer.PackStart(pRightEditor, True, True, 0)
                
                ' Add containers to paned
                pMainPaned.Pack1(pLeftContainer, True, False)
                pMainPaned.Pack2(pRightContainer, True, False)
                pMainPaned.Position = 400  ' Default split position
                
                ' Pack everything
                PackStart(pToolbar, False, False, 0)
                PackStart(pMainPaned, True, True, 0)
                
                ' Setup scroll synchronization
                SetupScrollSynchronization()
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"InitializeComponents error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Updated Public Methods to use SourceFileInfo =====
        
        

        
        ''' <summary>
        ''' Set whether editors are read-only
        ''' </summary>
        Public Sub SetReadOnly(vLeftReadOnly As Boolean, vRightReadOnly As Boolean)
            Try
                pLeftEditor.IsReadOnly = vLeftReadOnly
                pRightEditor.IsReadOnly = vRightReadOnly
            Catch ex As Exception
                Console.WriteLine($"SetReadOnly error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Load files into both editors for comparison
        ''' </summary>
        Public Sub LoadFiles(vLeftFilePath As String, vRightFilePath As String)
            Try
                ' Load left file
                If File.Exists(vLeftFilePath) Then
                    ' Update or create SourceFileInfo for left file
                    pLeftSourceFileInfo = GetOrCreateSourceFileInfo(vLeftFilePath)
                    If Not pLeftSourceFileInfo.IsLoaded Then
                        pLeftSourceFileInfo.LoadContent()
                    End If
                    
                    ' Recreate left editor with the loaded SourceFileInfo
                    pLeftContainer.Remove(pLeftEditor)
                    pLeftEditor = New CustomDrawingEditor(pLeftSourceFileInfo)
                    pLeftEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                    pLeftContainer.PackStart(pLeftEditor, True, True, 0)
                    
                    pLeftFileLabel.Text = System.IO.Path.GetFileName(vLeftFilePath)
                    pLeftFileLabel.TooltipText = vLeftFilePath
                End If
                
                ' Load right file
                If File.Exists(vRightFilePath) Then
                    ' Update or create SourceFileInfo for right file
                    pRightSourceFileInfo = GetOrCreateSourceFileInfo(vRightFilePath)
                    If Not pRightSourceFileInfo.IsLoaded Then
                        pRightSourceFileInfo.LoadContent()
                    End If
                    
                    ' Recreate right editor with the loaded SourceFileInfo
                    pRightContainer.Remove(pRightEditor)
                    pRightEditor = New CustomDrawingEditor(pRightSourceFileInfo)
                    pRightEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                    pRightContainer.PackStart(pRightEditor, True, True, 0)
                    
                    pRightFileLabel.Text = System.IO.Path.GetFileName(vRightFilePath)
                    pRightFileLabel.TooltipText = vRightFilePath
                End If
                
                ShowAll()
                
                ' Calculate differences
                CalculateDifferences()
                
                ' Update UI
                UpdateNavigationButtons()
                
            Catch ex As Exception
                Console.WriteLine($"LoadFiles error: {ex.Message}")
            End Try
        End Sub

        
        Private Sub CalculateDifferences()
            Try
                pDifferences.Clear()
                
                ' Simple line-by-line comparison
                ' TODO: Implement a proper diff algorithm (e.g., Myers' algorithm)
                Dim lLeftLines As String() = pLeftEditor.Text.Split({Environment.NewLine}, StringSplitOptions.None)
                Dim lRightLines As String() = pRightEditor.Text.Split({Environment.NewLine}, StringSplitOptions.None)
                
                Dim lMaxLines As Integer = Math.Max(lLeftLines.Length, lRightLines.Length)
                
                For i As Integer = 0 To lMaxLines - 1
                    Dim lLeftLine As String = If(i < lLeftLines.Length, lLeftLines(i), Nothing)
                    Dim lRightLine As String = If(i < lRightLines.Length, lRightLines(i), Nothing)
                    
                    If lLeftLine IsNot Nothing AndAlso lRightLine IsNot Nothing Then
                        ' Both sides have content
                        If lLeftLine <> lRightLine Then
                            ' Modified line
                            pDifferences.Add(New DifferenceInfo With {
                                .LeftStartLine = i,
                                .LeftEndLine = i,
                                .RightStartLine = i,
                                .RightEndLine = i,
                                .Type = DifferenceType.eModified
                            })
                        End If
                    ElseIf lLeftLine IsNot Nothing Then
                        ' Line deleted from right
                        pDifferences.Add(New DifferenceInfo With {
                            .LeftStartLine = i,
                            .LeftEndLine = i,
                            .RightStartLine = -1,
                            .RightEndLine = -1,
                            .Type = DifferenceType.eDeleted
                        })
                    ElseIf lRightLine IsNot Nothing Then
                        ' Line added to right
                        pDifferences.Add(New DifferenceInfo With {
                            .LeftStartLine = -1,
                            .LeftEndLine = -1,
                            .RightStartLine = i,
                            .RightEndLine = i,
                            .Type = DifferenceType.eAdded
                        })
                    End If
                Next
                
                ' TODO: Highlight differences in the editors
                
            Catch ex As Exception
                Console.WriteLine($"CalculateDifferences error: {ex.Message}")
            End Try
        End Sub

        Private Sub NavigateToDifference(vIndex As Integer)
            Try
                If vIndex < 0 OrElse vIndex >= pDifferences.Count Then
                    Return
                End If
                
                pCurrentDifferenceIndex = vIndex
                Dim lDiff As DifferenceInfo = pDifferences(vIndex)
                
                ' Scroll to difference in both editors
                If lDiff.LeftStartLine >= 0 Then
                    pLeftEditor.ScrollToLine(lDiff.LeftStartLine)
                End If
                
                If lDiff.RightStartLine >= 0 Then
                    pRightEditor.ScrollToLine(lDiff.RightStartLine)
                End If
                
                ' Raise event
                RaiseEvent DifferenceNavigated(vIndex, pDifferences.Count)
                
            Catch ex As Exception
                Console.WriteLine($"NavigateToDifference error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateToolbar()
            Try
                pToolbar = New Toolbar()
                pToolbar.ToolbarStyle = ToolbarStyle.Both
                
                ' Sync scrolling button
                pSyncScrollButton = New ToggleToolButton()
                pSyncScrollButton.Label = "Sync Scroll"
                pSyncScrollButton.IconName = "view-Refresh"
                pSyncScrollButton.TooltipText = "Synchronize scrolling between editors"
                pSyncScrollButton.Active = pSyncScrolling
                AddHandler pSyncScrollButton.Toggled, AddressOf OnSyncScrollToggled
                pToolbar.Add(pSyncScrollButton)
                
                ' Show differences button
                pShowDifferencesButton = New ToggleToolButton()
                pShowDifferencesButton.Label = "Highlight"
                pShowDifferencesButton.IconName = "format-Text-underline"
                pShowDifferencesButton.TooltipText = "Highlight differences"
                pShowDifferencesButton.Active = pShowDifferences
                AddHandler pShowDifferencesButton.Toggled, AddressOf OnShowDifferencesToggled
                pToolbar.Add(pShowDifferencesButton)
                
                pToolbar.Add(New SeparatorToolItem())
                
                ' Navigation buttons
                pPrevDiffButton = New ToolButton(Nothing, "GoUp")
                pPrevDiffButton.Label = "Previous"
                pPrevDiffButton.TooltipText = "Go to previous difference"
                pPrevDiffButton.Sensitive = False
                AddHandler pPrevDiffButton.Clicked, AddressOf OnPrevDifferenceClicked
                pToolbar.Add(pPrevDiffButton)
                
                pNextDiffButton = New ToolButton(Nothing, "GoDown")
                pNextDiffButton.Label = "Next"
                pNextDiffButton.TooltipText = "Go to next difference"
                pNextDiffButton.Sensitive = False
                AddHandler pNextDiffButton.Clicked, AddressOf OnNextDifferenceClicked
                pToolbar.Add(pNextDiffButton)
                
                pToolbar.Add(New SeparatorToolItem())
                
                ' Swap button
                pSwapButton = New ToolButton(Nothing, "Refresh")
                pSwapButton.Label = "Swap Files"
                pSwapButton.TooltipText = "Swap left and right files"
                AddHandler pSwapButton.Clicked, AddressOf OnSwapClicked
                pToolbar.Add(pSwapButton)
                
            Catch ex As Exception
                Console.WriteLine($"CreateToolbar error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateLeftHeader()
            Try
                pLeftHeader = New Box(Orientation.Horizontal, 5)
                pLeftHeader.BorderWidth = 5
                
                Dim lIcon As New Image()
                lIcon.SetFromIconName("Text-x-generic", IconSize.Menu)
                pLeftHeader.PackStart(lIcon, False, False, 0)
                
                pLeftFileLabel = New Label("No file loaded")
                pLeftFileLabel.Halign = Align.Start
                pLeftHeader.PackStart(pLeftFileLabel, True, True, 0)
                
            Catch ex As Exception
                Console.WriteLine($"CreateLeftHeader error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub CreateRightHeader()
            Try
                pRightHeader = New Box(Orientation.Horizontal, 5)
                pRightHeader.BorderWidth = 5
                
                Dim lIcon As New Image()
                lIcon.SetFromIconName("Text-x-generic", IconSize.Menu)
                pRightHeader.PackStart(lIcon, False, False, 0)
                
                pRightFileLabel = New Label("No file loaded")
                pRightFileLabel.Halign = Align.Start
                pRightHeader.PackStart(pRightFileLabel, True, True, 0)
                
            Catch ex As Exception
                Console.WriteLine($"CreateRightHeader error: {ex.Message}")
            End Try
        End Sub

        Private Sub UpdateNavigationButtons()
            Try
                Dim lHasDifferences As Boolean = pDifferences.Count > 0
                pPrevDiffButton.Sensitive = lHasDifferences
                pNextDiffButton.Sensitive = lHasDifferences
                
                If lHasDifferences AndAlso pCurrentDifferenceIndex = -1 Then
                    pCurrentDifferenceIndex = 0
                End If
                
            Catch ex As Exception
                Console.WriteLine($"UpdateNavigationButtons error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Load content directly for comparison (e.g., AI artifact vs original)
        ''' </summary>
        Public Sub LoadContent(vLeftContent As String, vLeftName As String, vRightContent As String, vRightName As String)
            Try
                ' Update left SourceFileInfo with content
                pLeftSourceFileInfo = New SourceFileInfo(vLeftName, "")
                pLeftSourceFileInfo.Content = vLeftContent
                pLeftSourceFileInfo.TextLines = New List(Of String)(vLeftContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                If pLeftSourceFileInfo.TextLines.Count = 0 Then
                    pLeftSourceFileInfo.TextLines.Add("")
                End If
                pLeftSourceFileInfo.IsLoaded = True
                
                ' Recreate left editor
                pLeftContainer.Remove(pLeftEditor)
                pLeftEditor = New CustomDrawingEditor(pLeftSourceFileInfo)
                pLeftEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                pLeftContainer.PackStart(pLeftEditor, True, True, 0)
                pLeftFileLabel.Text = vLeftName
                
                ' Update right SourceFileInfo with content
                pRightSourceFileInfo = New SourceFileInfo(vRightName, "")
                pRightSourceFileInfo.Content = vRightContent
                pRightSourceFileInfo.TextLines = New List(Of String)(vRightContent.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.None))
                If pRightSourceFileInfo.TextLines.Count = 0 Then
                    pRightSourceFileInfo.TextLines.Add("")
                End If
                pRightSourceFileInfo.IsLoaded = True
                
                ' Recreate right editor
                pRightContainer.Remove(pRightEditor)
                pRightEditor = New CustomDrawingEditor(pRightSourceFileInfo)
                pRightEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                pRightContainer.PackStart(pRightEditor, True, True, 0)
                pRightFileLabel.Text = vRightName
                
                ShowAll()
                
                ' Calculate differences
                CalculateDifferences()
                
                ' Update UI
                UpdateNavigationButtons()
                
            Catch ex As Exception
                Console.WriteLine($"LoadContent error: {ex.Message}")
            End Try
        End Sub

        ' ===== Private Helper Methods =====
        
        Private Sub SetupScrollSynchronization()
            Try
                ' TODO: Connect to the CustomDrawingEditor's scroll events when they're exposed
                ' For now, this is a placeholder for future implementation
                
            Catch ex As Exception
                Console.WriteLine($"SetupScrollSynchronization error: {ex.Message}")
            End Try
        End Sub        
        ' ===== Helper method to get or create SourceFileInfo =====
        Private Function GetOrCreateSourceFileInfo(vFilePath As String) As SourceFileInfo
            Try
                ' Try to get from ProjectManager if available
                If pProjectManager IsNot Nothing Then
                    Dim lExisting As SourceFileInfo = pProjectManager.GetSourceFileInfo(vFilePath)
                    If lExisting IsNot Nothing Then
                        Return lExisting
                    End If
                End If
                
                ' Create new SourceFileInfo
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(vFilePath)
                Return New SourceFileInfo(vFilePath, lProjectDir)
                
            Catch ex As Exception
                Console.WriteLine($"GetOrCreateSourceFileInfo error: {ex.Message}")
                Return New SourceFileInfo(vFilePath, "")
            End Try
        End Function
        
        ' ===== Event Handlers =====
        
        Private Sub OnSyncScrollToggled(vSender As Object, vArgs As EventArgs)
            Try
                pSyncScrolling = pSyncScrollButton.Active
            Catch ex As Exception
                Console.WriteLine($"OnSyncScrollToggled error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnShowDifferencesToggled(vSender As Object, vArgs As EventArgs)
            Try
                pShowDifferences = pShowDifferencesButton.Active
                ' TODO: Update highlighting in editors
            Catch ex As Exception
                Console.WriteLine($"OnShowDifferencesToggled error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnPrevDifferenceClicked(vSender As Object, vArgs As EventArgs)
            Try
                If pCurrentDifferenceIndex > 0 Then
                    NavigateToDifference(pCurrentDifferenceIndex - 1)
                ElseIf pDifferences.Count > 0 Then
                    ' Wrap around to last difference
                    NavigateToDifference(pDifferences.Count - 1)
                End If
            Catch ex As Exception
                Console.WriteLine($"OnPrevDifferenceClicked error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnNextDifferenceClicked(vSender As Object, vArgs As EventArgs)
            Try
                If pCurrentDifferenceIndex < pDifferences.Count - 1 Then
                    NavigateToDifference(pCurrentDifferenceIndex + 1)
                ElseIf pDifferences.Count > 0 Then
                    ' Wrap around to first difference
                    NavigateToDifference(0)
                End If
            Catch ex As Exception
                Console.WriteLine($"OnNextDifferenceClicked error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnSwapClicked(vSender As Object, vArgs As EventArgs)
            Try
                ' Swap the SourceFileInfo objects
                Dim lTempSourceFileInfo As SourceFileInfo = pLeftSourceFileInfo
                pLeftSourceFileInfo = pRightSourceFileInfo
                pRightSourceFileInfo = lTempSourceFileInfo
                
                ' Swap the labels
                Dim lTempLabel As String = pLeftFileLabel.Text
                Dim lTempTooltip As String = pLeftFileLabel.TooltipText
                
                pLeftFileLabel.Text = pRightFileLabel.Text
                pLeftFileLabel.TooltipText = pRightFileLabel.TooltipText
                
                pRightFileLabel.Text = lTempLabel
                pRightFileLabel.TooltipText = lTempTooltip
                
                ' Recreate editors with swapped SourceFileInfo
                ' Remove and recreate left editor
                pLeftContainer.Remove(pLeftEditor)
                pLeftEditor = New CustomDrawingEditor(pLeftSourceFileInfo)
                pLeftEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                pLeftContainer.PackStart(pLeftEditor, True, True, 0)
                
                ' Remove and recreate right editor
                pRightContainer.Remove(pRightEditor)
                pRightEditor = New CustomDrawingEditor(pRightSourceFileInfo)
                pRightEditor.SetDependencies(pSyntaxColorSet, pSettingsManager)
                pRightContainer.PackStart(pRightEditor, True, True, 0)
                
                ' Re-setup scroll synchronization
                SetupScrollSynchronization()
                
                ' Show all changes
                ShowAll()
                
                ' Recalculate differences
                CalculateDifferences()
                UpdateNavigationButtons()
                
                ' Raise event
                RaiseEvent FilesSwapped()
                
            Catch ex As Exception
                Console.WriteLine($"OnSwapClicked error: {ex.Message}")
            End Try
        End Sub

        
        ' ===== Styling =====
        Private Sub ApplyStyling()
            Try
                ' Apply CSS styling for headers
                Dim lHeaderCss As String = "
                    .comparison-header {
                        background-Color: #f5f5f5;
                        border-bottom: 1px solid #d0d0d0;
                        padding: 5px;
                    }
                    .comparison-header:backdrop {
                        background-Color: #eeeeee;
                    }
                "
                
                CssHelper.ApplyCssToWidget(pLeftHeader, lHeaderCss, "comparison-header")
                CssHelper.ApplyCssToWidget(pRightHeader, lHeaderCss, "comparison-header")
                
                ' Style the paned separator
                Dim lPanedCss As String = "
                    paned > separator {
                        background-Color: #d0d0d0;
                        background-image: none;
                        min-Width: 5px;
                    }
                    paned > separator:hover {
                        background-Color: #b0b0b0;
                    }
                "
                
                CssHelper.ApplyCssToWidget(pMainPaned, lPanedCss, CssHelper.STYLE_PROVIDER_PRIORITY_USER)
                
            Catch ex As Exception
                Console.WriteLine($"ApplyStyling error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
