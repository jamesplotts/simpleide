' Editors/CustomDrawingEditor.ContextMenu.vb - Context menu implementation
Imports Gtk
Imports System
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models

Namespace Editors
    
    Partial Public Class CustomDrawingEditor
        Inherits Box
        Implements IEditor
        
        ' ===== Context Menu Fields =====
        Private pContextMenu As Menu
        Private pLineNumberContextMenu As Menu
        Private pLastRightClickX As Double
        Private pLastRightClickY As Double
        Private pLastRightClickInLineNumbers As Boolean

        ' ===== Event Declaration =====
        Public Event GoToLineRequested()

        
        ' ===== Context Menu Initialization =====

        Private Sub InitializeContextMenus()
            Try
                CreateTextAreaContextMenu()
                CreateLineNumberContextMenu()
                
            Catch ex As Exception
                Console.WriteLine($"InitializeContextMenus error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Text Area Context Menu =====

        Private Sub CreateTextAreaContextMenu()
            Try
                pContextMenu = New Menu()
                
                ' Cut menu item
                Dim lCutItem As New MenuItem("Cu_t")
                lCutItem.Name = "CutMenuItem"
                AddHandler lCutItem.Activated, AddressOf OnContextMenuCut
                pContextMenu.Append(lCutItem)
                
                ' Copy menu item
                Dim lCopyItem As New MenuItem("_Copy")
                lCopyItem.Name = "CopyMenuItem"
                AddHandler lCopyItem.Activated, AddressOf OnContextMenuCopy
                pContextMenu.Append(lCopyItem)
                
                ' Paste menu item
                Dim lPasteItem As New MenuItem("_Paste")
                lPasteItem.Name = "PasteMenuItem"
                AddHandler lPasteItem.Activated, AddressOf OnContextMenuPaste
                pContextMenu.Append(lPasteItem)
                
                ' Smart Paste menu item (NEW)
                Dim lSmartPasteItem As New MenuItem("Smart Paste")
                lSmartPasteItem.Name = "SmartPasteMenuItem"
                lSmartPasteItem.TooltipText = "Paste with artifact comment stripping and auto-indentation (Ctrl+Shift+V)"
                AddHandler lSmartPasteItem.Activated, AddressOf OnContextMenuSmartPaste
                pContextMenu.Append(lSmartPasteItem)
                
                ' Separator
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Select All menu item
                Dim lSelectAllItem As New MenuItem("Select _All")
                lSelectAllItem.Name = "SelectAllMenuItem"
                AddHandler lSelectAllItem.Activated, AddressOf OnContextMenuSelectAll
                pContextMenu.Append(lSelectAllItem)
                
                ' Separator
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Find menu item
                Dim lFindItem As New MenuItem("_Find...")
                lFindItem.Name = "FindMenuItem"
                AddHandler lFindItem.Activated, AddressOf OnContextMenuFind
                pContextMenu.Append(lFindItem)
                
                ' Replace menu item
                Dim lReplaceItem As New MenuItem("_Replace...")
                lReplaceItem.Name = "ReplaceMenuItem"
                AddHandler lReplaceItem.Activated, AddressOf OnContextMenuReplace
                pContextMenu.Append(lReplaceItem)
                
                ' Separator
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Go to Line menu item
                Dim lGoToLineItem As New MenuItem("_Go to Line...")
                lGoToLineItem.Name = "GoToLineMenuItem"
                AddHandler lGoToLineItem.Activated, AddressOf OnContextMenuGoToLine
                pContextMenu.Append(lGoToLineItem)
                
                ' Conditional separator (shown only when needed)
                Dim lConditionalSeparator As New SeparatorMenuItem()
                lConditionalSeparator.Name = "ConditionalSeparator"
                pContextMenu.Append(lConditionalSeparator)
                
                ' Go to Definition menu item (conditional)
                Dim lGoToDefinitionItem As New MenuItem("Go to _Definition")
                lGoToDefinitionItem.Name = "GoToDefinitionMenuItem"
                AddHandler lGoToDefinitionItem.Activated, AddressOf OnContextMenuGoToDefinition
                pContextMenu.Append(lGoToDefinitionItem)
                
                ' Show all items
                pContextMenu.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"CreateTextAreaContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Line Number Area Context Menu =====
        Private Sub CreateLineNumberContextMenu()
            Try
                pLineNumberContextMenu = New Menu()
                
                ' Select Block menu item
                Dim lSelectBlockItem As New MenuItem("Select _Block")
                lSelectBlockItem.Name = "SelectBlockMenuItem"
                AddHandler lSelectBlockItem.Activated, AddressOf OnContextMenuSelectBlock
                pLineNumberContextMenu.Append(lSelectBlockItem)
                
                ' Go To Line Number menu item
                Dim lGoToLineItem As New MenuItem("_Go To Line Number...")
                lGoToLineItem.Name = "GoToLineMenuItem"
                AddHandler lGoToLineItem.Activated, AddressOf OnContextMenuGoToLine
                pLineNumberContextMenu.Append(lGoToLineItem)
                
                ' Separator
                pLineNumberContextMenu.Append(New SeparatorMenuItem())
                
                ' Toggle Breakpoint menu item
                Dim lToggleBreakpointItem As New MenuItem("Toggle _Breakpoint")
                lToggleBreakpointItem.Name = "ToggleBreakpointMenuItem"
                AddHandler lToggleBreakpointItem.Activated, AddressOf OnContextMenuToggleBreakpoint
                pLineNumberContextMenu.Append(lToggleBreakpointItem)
                
                ' Insert Line Above menu item
                Dim lInsertLineAboveItem As New MenuItem("Insert Line _Above")
                lInsertLineAboveItem.Name = "InsertLineAboveMenuItem"
                AddHandler lInsertLineAboveItem.Activated, AddressOf OnContextMenuInsertLineAbove
                pLineNumberContextMenu.Append(lInsertLineAboveItem)
                
                ' Insert Line Below menu item
                Dim lInsertLineBelowItem As New MenuItem("Insert Line _Below")
                lInsertLineBelowItem.Name = "InsertLineBelowMenuItem"
                AddHandler lInsertLineBelowItem.Activated, AddressOf OnContextMenuInsertLineBelow
                pLineNumberContextMenu.Append(lInsertLineBelowItem)
                
            Catch ex As Exception
                Console.WriteLine($"CreateLineNumberContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Context Menu Show Methods =====
        Private Sub ShowTextAreaContextMenu(vX As Double, vY As Double)
            Try
                pLastRightClickX = vX
                pLastRightClickY = vY
                pLastRightClickInLineNumbers = False
                
                ' Update menu item states based on current state
                UpdateTextAreaContextMenuStates()
                
                ' Show the menu
                pContextMenu.ShowAll()
                pContextMenu.PopupAtPointer(Nothing)
                
            Catch ex As Exception
                Console.WriteLine($"ShowTextAreaContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ShowLineNumberContextMenu(vX As Double, vY As Double)
            Try
                pLastRightClickX = vX
                pLastRightClickY = vY
                pLastRightClickInLineNumbers = True
                
                ' Update menu item states
                UpdateLineNumberContextMenuStates()
                
                ' Show the menu
                pLineNumberContextMenu.ShowAll()
                pLineNumberContextMenu.PopupAtPointer(Nothing)
                
            Catch ex As Exception
                Console.WriteLine($"ShowLineNumberContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Context Menu State Updates =====
        Private Sub UpdateTextAreaContextMenuStates()
            Try
                Dim lHasSelection As Boolean = pHasSelection
                Dim lHasClipboard As Boolean = CheckClipboardHasText()
                Dim lSelectedWord As String = ""
                
                ' Get selected word if there's a selection
                If lHasSelection Then
                    lSelectedWord = GetSelectedText()
                End If
                
                ' Update Cut/Copy availability
                for each lChild As Widget in pContextMenu.Children
                    If TypeOf lChild Is MenuItem Then
                        Dim lMenuItem As MenuItem = CType(lChild, MenuItem)
                        
                        Select Case lMenuItem.Name

                            Case "CutMenuItem", "CopyMenuItem"
                                lMenuItem.Sensitive = lHasSelection
                                
                            Case "PasteMenuItem"
                                lMenuItem.Sensitive = lHasClipboard AndAlso Not pIsReadOnly
                                
                            Case "GoToDefinitionMenuItem"
                                ' Show Go To Definition only if we have a selected identifier
                                Dim lShowGoToDef As Boolean = lHasSelection AndAlso IsValidIdentifier(lSelectedWord.Trim())
                                lMenuItem.Visible = lShowGoToDef
                                
                            Case "ConditionalSeparator"
                                ' Show separator only if we have conditional items visible
                                Dim lHasConditionalItems As Boolean = lHasSelection AndAlso IsValidIdentifier(lSelectedWord.Trim())
                                lMenuItem.Visible = lHasConditionalItems

                            Case "SmartPasteMenuItem"
                                lMenuItem.Sensitive = lHasClipboard AndAlso Not pIsReadOnly

                        End Select
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"UpdateTextAreaContextMenuStates error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub UpdateLineNumberContextMenuStates()
            Try
                ' All line number context menu items are generally always available
                ' but we could disable some based on context if needed
                for each lChild As Widget in pLineNumberContextMenu.Children
                    If TypeOf lChild Is MenuItem Then
                        Dim lMenuItem As MenuItem = CType(lChild, MenuItem)
                        
                        Select Case lMenuItem.Name
                            Case "InsertLineAboveMenuItem", "InsertLineBelowMenuItem"
                                lMenuItem.Sensitive = Not pIsReadOnly
                        End Select
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"UpdateLineNumberContextMenuStates error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Helper Methods =====
        Private Function CheckClipboardHasText() As Boolean
            Try
                Dim lClipboard As Clipboard = Clipboard.Get(Gdk.Selection.Clipboard)
                Return lClipboard.WaitIsTextAvailable()
                
            Catch ex As Exception
                Console.WriteLine($"CheckClipboardHasText error: {ex.Message}")
                Return False
            End Try
        End Function
        
        Private Function IsValidIdentifier(vText As String) As Boolean
            Try
                If String.IsNullOrWhiteSpace(vText) Then Return False
                
                ' Basic identifier check - starts with letter or underscore, contains only letters, digits, underscores
                If Not Char.IsLetter(vText(0)) AndAlso vText(0) <> "_"c Then Return False
                
                for each lChar As Char in vText
                    If Not (Char.IsLetterOrDigit(lChar) OrElse lChar = "_"c) Then
                        Return False
                    End If
                Next
                
                ' Don't show for VB.NET keywords
                Dim lKeywords As String() = {"if", "then", "else", "end", "sub", "function", "class", "module", "namespace", 
                                           "public", "private", "protected", "friend", "shared", "dim", "as", "string", 
                                           "integer", "boolean", "double", "single", "date", "object", "nothing", "true", "false"}
                
                Return Not lKeywords.Contains(vText.ToLower())
                
            Catch ex As Exception
                Console.WriteLine($"IsValidIdentifier error: {ex.Message}")
                Return False
            End Try
        End Function
        
        ' ===== Context Menu Event Handlers =====
        
        ' Text Area Context Menu Handlers
        Private Sub OnContextMenuCut(vSender As Object, vArgs As EventArgs)
            Try
                If pHasSelection AndAlso Not pIsReadOnly Then
                    Copy()
                    DeleteSelection()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuCut error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuCopy(vSender As Object, vArgs As EventArgs)
            Try
                If pHasSelection Then
                    Copy()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuCopy error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuPaste(vSender As Object, vArgs As EventArgs)
            Try
                If Not pIsReadOnly Then
                    Paste()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuPaste error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuSelectAll(vSender As Object, vArgs As EventArgs)
            Try
                SelectAll()
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuSelectAll error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuGoToDefinition(vSender As Object, vArgs As EventArgs)
            Try
                Dim lSelectedText As String = GetSelectedText()
                If Not String.IsNullOrWhiteSpace(lSelectedText) Then
                    ' TODO: Implement Go To Definition functionality
                    ' This would integrate with the project manager to find symbol definitions
                    Console.WriteLine($"Go To Definition requested for: {lSelectedText.Trim()}")
                    
                    ' For now, show a placeholder message
                    Dim lDialog As New MessageDialog(
                        Nothing,
                        DialogFlags.Modal,
                        MessageType.Info,
                        ButtonsType.Ok,
                        $"Go To Definition for '{lSelectedText.Trim()}' is not yet implemented.")
                    lDialog.Run()
                    lDialog.Destroy()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuGoToDefinition error: {ex.Message}")
            End Try
        End Sub
        
        ' Line Number Context Menu Handlers
        Private Sub OnContextMenuSelectBlock(vSender As Object, vArgs As EventArgs)
            Try
                ' Get the line number where the right-click occurred
                Dim lLine As Integer = GetLineFromY(pLastRightClickY)
                If lLine >= 0 AndAlso lLine < pLineCount Then
                    ' Find the block boundaries (simple implementation)
                    Dim lStartLine As Integer = FindBlockStart(lLine)
                    Dim lEndLine As Integer = FindBlockEnd(lLine)
                    
                    ' Select the entire block
                    If lStartLine <= lEndLine Then
                        SelectLines(lStartLine, lEndLine)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuSelectBlock error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuGoToLine(vSender As Object, vArgs As EventArgs)
            Try
                ' Use the existing Go To Line dialog from MainWindow
                ' We need to raise an event or call a delegate since we don't have direct access to MainWindow
                RaiseEvent GoToLineRequested()
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuGoToLine error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuToggleBreakpoint(vSender As Object, vArgs As EventArgs)
            Try
                Dim lLine As Integer = GetLineFromY(pLastRightClickY)
                If lLine >= 0 AndAlso lLine < pLineCount Then
                    ' TODO: Implement breakpoint functionality
                    Console.WriteLine($"Toggle breakpoint at line {lLine + 1}")
                    
                    ' For now, show a placeholder
                    Dim lDialog As New MessageDialog(
                        Nothing,
                        DialogFlags.Modal,
                        MessageType.Info,
                        ButtonsType.Ok,
                        $"Breakpoint functionality Is Not yet implemented.{Environment.NewLine}Line: {lLine + 1}")
                    lDialog.Run()
                    lDialog.Destroy()
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuToggleBreakpoint error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuInsertLineAbove(vSender As Object, vArgs As EventArgs)
            Try
                If pIsReadOnly Then Return
                
                Dim lLine As Integer = GetLineFromY(pLastRightClickY)
                If lLine >= 0 AndAlso lLine < pLineCount Then
                    ' Insert a new line above the clicked line
                    InsertTextAtPosition(New EditorPosition(lLine, 0), Environment.NewLine)
                    ' Position cursor at the new line
                    SetCursorPosition(New EditorPosition(lLine, 0))
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuInsertLineAbove error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuInsertLineBelow(vSender As Object, vArgs As EventArgs)
            Try
                If pIsReadOnly Then Return
                
                Dim lLine As Integer = GetLineFromY(pLastRightClickY)
                If lLine >= 0 AndAlso lLine < pLineCount Then
                    ' Insert a new line below the clicked line
                    Dim lLineLength As Integer = TextLines(lLine).Length
                    InsertTextAtPosition(New EditorPosition(lLine, lLineLength), Environment.NewLine)
                    ' Position cursor at the new line
                    SetCursorPosition(lLine + 1, 0)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuInsertLineBelow error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Block Detection Helpers =====
        Private Function FindBlockStart(vLine As Integer) As Integer
            Try
                ' Simple block detection - find previous line with same or less indentation
                If vLine < 0 OrElse vLine >= pLineCount Then Return vLine
                
                Dim lCurrentIndent As Integer = GetLineIndentationLevel(vLine)
                Dim lStartLine As Integer = vLine
                
                ' Look backwards for a line with less indentation or block start keywords
                For i As Integer = vLine - 1 To 0 Step -1
                    Dim lLineText As String = TextLines(i).Trim().ToLower()
                    Dim lIndent As Integer = GetLineIndentationLevel(i)
                    
                    ' If this line has less indentation, it might be the block start
                    If lIndent < lCurrentIndent Then
                        lStartLine = i
                        Exit For
                    End If
                    
                    ' Check for block keywords
                    If lLineText.StartsWith("Sub ") OrElse lLineText.StartsWith("Function ") OrElse 
                       lLineText.StartsWith("If ") OrElse lLineText.StartsWith("Class ") OrElse
                       lLineText.StartsWith("Module ") OrElse lLineText.StartsWith("Namespace ") Then
                        lStartLine = i
                        Exit For
                    End If
                Next
                
                Return lStartLine
                
            Catch ex As Exception
                Console.WriteLine($"FindBlockStart error: {ex.Message}")
                Return vLine
            End Try
        End Function
        
        Private Function FindBlockEnd(vLine As Integer) As Integer
            Try
                ' Simple block detection - find next line with same or less indentation
                If vLine < 0 OrElse vLine >= pLineCount Then Return vLine
                
                Dim lCurrentIndent As Integer = GetLineIndentationLevel(vLine)
                Dim lEndLine As Integer = vLine
                
                ' Look forwards for a line with less or equal indentation or block end keywords
                For i As Integer = vLine + 1 To pLineCount - 1
                    Dim lLineText As String = TextLines(i).Trim().ToLower()
                    Dim lIndent As Integer = GetLineIndentationLevel(i)
                    
                    ' Check for explicit end keywords first (these ARE part of the block)
                    If lLineText.StartsWith("End ") OrElse lLineText = "End" OrElse
                       lLineText.StartsWith("Next") OrElse lLineText.StartsWith("Loop") OrElse
                       lLineText.StartsWith("Wend") OrElse lLineText.StartsWith("until") Then
                        lEndLine = i  ' Include the end statement
                        Exit For
                    End If
                    
                    ' If this line has less indentation and it's not empty, we've found the end of the block
                    If lIndent < lCurrentIndent AndAlso Not String.IsNullOrWhiteSpace(lLineText) Then
                        lEndLine = i - 1 ' Previous line was the actual end
                        Exit For
                    End If
                    
                    ' If this line has equal indentation and it's a new statement (not continuation), end the block
                    If lIndent = lCurrentIndent AndAlso Not String.IsNullOrWhiteSpace(lLineText) Then
                        ' Check if it's a continuation of the same block or a new statement
                        If IsNewStatementStart(lLineText) Then
                            lEndLine = i - 1 ' Previous line was the actual end
                            Exit For
                        End If
                    End If
                    
                    lEndLine = i ' Keep extending if no end found
                Next
                
                Return lEndLine
                
            Catch ex As Exception
                Console.WriteLine($"FindBlockEnd error: {ex.Message}")
                Return vLine
            End Try
        End Function
        
        Private Function GetLineIndentationLevel(vLine As Integer) As Integer
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return 0
                
                Dim lLine As String = TextLines(vLine)
                Dim lIndent As Integer = 0
                
                For Each lChar As Char In lLine
                    If lChar = " "c Then
                        lIndent += 1
                    ElseIf lChar = vbTab Then
                        lIndent += 4 ' Assume tab = 4 spaces
                    Else
                        Exit For
                    End If
                Next
                
                Return lIndent
                
            Catch ex As Exception
                Console.WriteLine($"GetLineIndentationLevel error: {ex.Message}")
                Return 0
            End Try
        End Function
        
        ''' <summary>
        ''' Check if a line starts a new statement (vs. being part of a multi-line statement)
        ''' </summary>
        Private Function IsNewStatementStart(vLineText As String) As Boolean
            Try
                If String.IsNullOrWhiteSpace(vLineText) Then Return False
                
                ' VB.NET statement starters
                Dim lStatementStarters As String() = {
                    "Dim ", "Private ", "Public ", "Protected ", "Friend ", "Shared ",
                    "Const ", "Static ",
                    "If ", "for ", "While ", "Do ", "Select ", "Try ", "with ", "Using ",
                    "Sub ", "Function ", "Property ", "Class ", "Module ", "Interface ",
                    "Namespace ", "Imports ", "Option ",
                    "Return", "Exit ", "Continue ", "Throw ", "GoTo ",
                    "Call ", "Set ", "Get ",
                    "Else", "ElseIf ", "Case ", "Catch ", "Finally "
                }
                
                Dim lLowerLine As String = vLineText.ToLower()
                For Each lStarter In lStatementStarters
                    If lLowerLine.StartsWith(lStarter) Then Return True
                Next
                
                ' Also check for variable assignments (contains "=")
                If lLowerLine.Contains("=") AndAlso Not lLowerLine.Contains("==") AndAlso Not lLowerLine.Contains("<=") AndAlso Not lLowerLine.Contains(">=") AndAlso Not lLowerLine.Contains("<>") Then
                    Return True
                End If
                
                Return False
                
            Catch ex As Exception
                Console.WriteLine($"IsNewStatementStart error: {ex.Message}")
                Return False
            End Try
        End Function

        ''' <summary>
        ''' Handles Smart Paste context menu item click
        ''' </summary>
        Private Sub OnContextMenuSmartPaste(vSender As Object, vArgs As EventArgs)
            Try
                SmartPaste()
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuSmartPaste error: {ex.Message}")
            End Try
        End Sub
        
        
    End Class
    
End Namespace
