' Editors/CustomDrawingEditor.ContextMenu.vb - Context menu implementation
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Interfaces
Imports SimpleIDE.Utilities
Imports SimpleIDE.Models
Imports SimpleIDE.Syntax

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
        ''' <summary>
        ''' Raised when the text-area context menu's "Go to Line..." item is used, since this
        ''' editor doesn't have direct access to MainWindow's Go To Line dialog
        ''' </summary>
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

                ' Find All References menu item
                Dim lFindAllReferencesItem As New MenuItem("Find All _References")
                lFindAllReferencesItem.Name = "FindAllReferencesMenuItem"
                AddHandler lFindAllReferencesItem.Activated, AddressOf OnContextMenuFindAllReferences
                pContextMenu.Append(lFindAllReferencesItem)

                ' Generate Field(s) From Parameters menu item (conditional - only shown when
                ' the cursor is inside a Sub/Function/constructor with a parameter that
                ' doesn't already have a matching field)
                Dim lGenerateFieldsItem As New MenuItem("_Generate Field(s) From Parameters")
                lGenerateFieldsItem.Name = "GenerateFieldsMenuItem"
                AddHandler lGenerateFieldsItem.Activated, AddressOf OnContextMenuGenerateFieldsFromParameters
                pContextMenu.Append(lGenerateFieldsItem)

                ' Implement Interface Members menu item (conditional - only shown when the
                ' cursor is inside a class/module that Implements an interface with members
                ' not yet implemented)
                Dim lImplementInterfaceItem As New MenuItem("Imple_ment Interface Members")
                lImplementInterfaceItem.Name = "ImplementInterfaceMenuItem"
                AddHandler lImplementInterfaceItem.Activated, AddressOf OnContextMenuImplementInterface
                pContextMenu.Append(lImplementInterfaceItem)

                ' Add Missing Doc Tags menu item (conditional - only shown when the right-click
                ' landed on an XML doc comment block whose declaration has a parameter (or,
                ' for a Function/Property, a return value) not yet documented)
                Dim lAddMissingDocTagsItem As New MenuItem("Add _Missing Doc Tags")
                lAddMissingDocTagsItem.Name = "AddMissingDocTagsMenuItem"
                AddHandler lAddMissingDocTagsItem.Activated, AddressOf OnContextMenuAddMissingDocTags
                pContextMenu.Append(lAddMissingDocTagsItem)

                ' Surround Selection With submenu (conditional - only shown when there's a
                ' selection)
                Dim lSurroundWithItem As New MenuItem("Su_rround Selection With")
                lSurroundWithItem.Name = "SurroundWithMenuItem"
                Dim lSurroundWithMenu As New Menu()
                lSurroundWithItem.Submenu = lSurroundWithMenu

                Dim lSurroundKinds As New Dictionary(Of String, SurroundWithKind) From {
                    {"Try / Catch", SurroundWithKind.eTryCatch},
                    {"If", SurroundWithKind.eIf},
                    {"For", SurroundWithKind.eFor},
                    {"For Each", SurroundWithKind.eForEach},
                    {"Using", SurroundWithKind.eUsing},
                    {"With", SurroundWithKind.eWith},
                    {"While", SurroundWithKind.eWhile}
                }
                for each lEntry In lSurroundKinds
                    Dim lKindItem As New MenuItem(lEntry.Key)
                    Dim lKind As SurroundWithKind = lEntry.Value
                    AddHandler lKindItem.Activated, Sub(vSender As Object, vArgs As EventArgs) SurroundSelectionWith(lKind)
                    lSurroundWithMenu.Append(lKindItem)
                Next

                pContextMenu.Append(lSurroundWithItem)

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

                ' Separator
                pLineNumberContextMenu.Append(New SeparatorMenuItem())

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
        
        Public Sub ShowLineNumberContextMenu(vX As Double, vY As Double)
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

                ' Resolve the identifier that Go To Definition would actually act on:
                ' the selection if there is one, otherwise the word at the click position -
                ' keeps the menu's visibility consistent with what the action will do
                Dim lGoToDefWord As String = lSelectedWord.Trim()
                If lHasSelection Then
                    ' lGoToDefWord already set above
                Else
                    Dim lClickLine, lClickColumn As Integer
                    lGoToDefWord = GetWordAtClickPosition(lClickLine, lClickColumn)
                End If
                Dim lHasGoToDefTarget As Boolean = IsValidIdentifier(lGoToDefWord)

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
                                ' Show Go To Definition if there's a selected identifier, or a
                                ' valid identifier under the mouse at the click position
                                lMenuItem.Visible = lHasGoToDefTarget

                            Case "ConditionalSeparator"
                                ' Show separator only if we have conditional items visible
                                lMenuItem.Visible = lHasGoToDefTarget

                            Case "SmartPasteMenuItem"
                                lMenuItem.Sensitive = lHasClipboard AndAlso Not pIsReadOnly

                            Case "GenerateFieldsMenuItem"
                                lMenuItem.Visible = GetGenerateFieldCandidates().Count > 0

                            Case "ImplementInterfaceMenuItem"
                                lMenuItem.Visible = GetUnimplementedInterfaceMembers().Count > 0

                            Case "AddMissingDocTagsMenuItem"
                                Dim lClickLine As Integer = GetLineFromY(pLastRightClickY)
                                lMenuItem.Visible = FindMissingDocTags(lClickLine) IsNot Nothing

                            Case "SurroundWithMenuItem"
                                lMenuItem.Visible = lHasSelection

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
        
        ''' <summary>
        ''' Finds the identifier/keyword/type word at the last right-click position
        ''' (pLastRightClickX/Y), independent of the text cursor and any selection - right-
        ''' clicking never moves the cursor, so resolving from the cursor here would act on
        ''' whatever was last clicked/typed rather than what's actually under the pointer
        ''' </summary>
        ''' <param name="vLine">Receives the 0-based line the click landed on</param>
        ''' <param name="vColumn">Receives the 0-based column the found word starts at</param>
        ''' <returns>The word at the click position, or "" if it wasn't on a recognized token</returns>
        ''' <remarks>
        ''' Shared by OnContextMenuGoToDefinition (so the action always resolves the same word
        ''' the menu was shown for) and UpdateTextAreaContextMenuStates (so the menu item's
        ''' visibility matches what clicking it will actually do)
        ''' </remarks>
        Private Function GetWordAtClickPosition(ByRef vLine As Integer, ByRef vColumn As Integer) As String
            vLine = 0
            vColumn = 0
            Try
                Dim lClickPos As EditorPosition = GetPositionFromCoordinates(pLastRightClickX, pLastRightClickY)
                vLine = lClickPos.Line
                If vLine < 0 OrElse vLine >= TextLines.Count Then Return ""

                Dim lLine As String = TextLines(vLine)
                Dim lTokenizer As New VBTokenizer()
                Dim lTokens As List(Of Token) = lTokenizer.TokenizeLine(lLine)

                For Each lToken In lTokens
                    If lClickPos.Column >= lToken.StartColumn AndAlso lClickPos.Column <= lToken.EndColumn Then
                        If lToken.Type = TokenType.eIdentifier OrElse
                           lToken.Type = TokenType.eKeyword OrElse
                           lToken.Type = TokenType.eType Then
                            vColumn = lToken.StartColumn
                            Return lToken.Text
                        End If
                    End If
                Next

                Return ""

            Catch ex As Exception
                Console.WriteLine($"GetWordAtClickPosition error: {ex.Message}")
                Return ""
            End Try
        End Function

        ''' <summary>
        ''' Handles the Go to Definition context menu item click
        ''' </summary>
        ''' <param name="vSender">The menu item that was clicked</param>
        ''' <param name="vArgs">Event arguments</param>
        ''' <remarks>
        ''' Extracts the word at the cursor position and raises the RequestGotoDefinition event
        ''' </remarks>
        Private Sub OnContextMenuGoToDefinition(vSender As Object, vArgs As EventArgs)
            Try
                Console.WriteLine("OnContextMenuGoToDefinition: Started")

                ' First try to get selected text
                Dim lSelectedText As String = GetSelectedText()
                Dim lWord As String = ""
                Dim lLineNumber As Integer = 0
                Dim lColumnNumber As Integer = 0

                If Not String.IsNullOrWhiteSpace(lSelectedText) Then
                    ' Use the selected text as the word
                    lWord = lSelectedText.Trim()
                    lLineNumber = pSelectionStartLine
                    lColumnNumber = Math.Min(pSelectionStartColumn, pSelectionEndColumn)
                    Console.WriteLine($"OnContextMenuGoToDefinition: Using selected text '{lWord}'")
                Else
                    ' No selection - resolve the word under the mouse at the right-click
                    ' location, not the text cursor
                    lWord = GetWordAtClickPosition(lLineNumber, lColumnNumber)
                    Console.WriteLine($"OnContextMenuGoToDefinition: No selection, word at click = '{lWord}' at {lLineNumber}:{lColumnNumber}")
                End If

                ' Check if we found a word
                If String.IsNullOrWhiteSpace(lWord) Then
                    Console.WriteLine("OnContextMenuGoToDefinition: No word found at click position")
                    Return
                End If

                ' Create event arguments
                Dim lEventArgs As New GoToDefinitionEventArgs()
                lEventArgs.FilePath = pSourceFileInfo.FilePath
                lEventArgs.LineNumber = lLineNumber
                lEventArgs.ColumnNumber = lColumnNumber
                lEventArgs.Word = lWord

                Console.WriteLine($"OnContextMenuGoToDefinition: Raising event for word '{lWord}' at {lEventArgs.FilePath}:{lEventArgs.LineNumber}:{lEventArgs.ColumnNumber}")

                ' Raise the event
                RaiseEvent RequestGotoDefinition(Me, lEventArgs)

            Catch ex As Exception
                Console.WriteLine($"OnContextMenuGoToDefinition error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles the "Find All References" context menu item click
        ''' </summary>
        ''' <param name="vSender">The menu item that was clicked</param>
        ''' <param name="vArgs">Event arguments</param>
        ''' <remarks>
        ''' Resolves the word exactly like Go to Definition does, then raises
        ''' RequestFindAllReferences instead of RequestGotoDefinition
        ''' </remarks>
        Private Sub OnContextMenuFindAllReferences(vSender As Object, vArgs As EventArgs)
            Try
                Console.WriteLine("OnContextMenuFindAllReferences: Started")

                Dim lSelectedText As String = GetSelectedText()
                Dim lWord As String = ""
                Dim lLineNumber As Integer = 0
                Dim lColumnNumber As Integer = 0

                If Not String.IsNullOrWhiteSpace(lSelectedText) Then
                    lWord = lSelectedText.Trim()
                    lLineNumber = pSelectionStartLine
                    lColumnNumber = Math.Min(pSelectionStartColumn, pSelectionEndColumn)
                Else
                    lWord = GetWordAtClickPosition(lLineNumber, lColumnNumber)
                End If

                If String.IsNullOrWhiteSpace(lWord) Then
                    Console.WriteLine("OnContextMenuFindAllReferences: No word found at click position")
                    Return
                End If

                Dim lEventArgs As New GoToDefinitionEventArgs()
                lEventArgs.FilePath = pSourceFileInfo.FilePath
                lEventArgs.LineNumber = lLineNumber
                lEventArgs.ColumnNumber = lColumnNumber
                lEventArgs.Word = lWord

                RaiseEvent RequestFindAllReferences(Me, lEventArgs)

            Catch ex As Exception
                Console.WriteLine($"OnContextMenuFindAllReferences error: {ex.Message}")
                Console.WriteLine($"Stack trace: {ex.StackTrace}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles the Generate Field(s) From Parameters context menu item click
        ''' </summary>
        Private Sub OnContextMenuGenerateFieldsFromParameters(vSender As Object, vArgs As EventArgs)
            Try
                GenerateFieldsFromParameters()
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuGenerateFieldsFromParameters error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Handles the Implement Interface Members context menu item click
        ''' </summary>
        Private Sub OnContextMenuImplementInterface(vSender As Object, vArgs As EventArgs)
            Try
                ImplementInterfaceMembers()
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuImplementInterface error: {ex.Message}")
            End Try
        End Sub

        ' Line Number Context Menu Handlers
        Private Sub OnContextMenuSelectBlock(vSender As Object, vArgs As EventArgs)
            Try
                ' Get the line number where the right-click occurred
                Dim lLine As Integer = GetLineFromY(pLastRightClickY)
                If lLine >= 0 AndAlso lLine < pLineCount Then
                    SelectContainingBlock(lLine)
                End If

            Catch ex As Exception
                Console.WriteLine($"OnContextMenuSelectBlock error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Selects the most specific enclosing declaration (method, property, class, etc.)
        ''' containing vLine, including any XML doc comment immediately above it. Falls back to
        ''' selecting just vLine if nothing encloses it. Shared by the gutter's "Select Block"
        ''' context menu item and double-clicking a line number.
        ''' </summary>
        ''' <param name="vLine">0-based source line to select the containing block for</param>
        Public Sub SelectContainingBlock(vLine As Integer)
            Try
                If vLine < 0 OrElse vLine >= pLineCount Then Return

                Dim lBlockNode As SyntaxNode = FindContainingBlockNode(pRootNode, vLine)
                If lBlockNode IsNot Nothing Then
                    Dim lStartLine As Integer = FindXmlDocStartLine(lBlockNode.StartLine)
                    SelectLines(lStartLine, lBlockNode.EndLine)
                Else
                    ' No enclosing declaration found (e.g. a blank line outside any
                    ' type/namespace) - just select the clicked line
                    SelectLines(vLine, vLine)
                End If

            Catch ex As Exception
                Console.WriteLine($"SelectContainingBlock error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Walks upward from vDeclarationLine while the immediately preceding lines are XML
        ''' doc comments (''' ...), so a block selection includes its documentation
        ''' </summary>
        Private Function FindXmlDocStartLine(vDeclarationLine As Integer) As Integer
            Dim lStartLine As Integer = vDeclarationLine
            for i As Integer = vDeclarationLine - 1 To 0 Step -1
                If i >= pLineCount Then Continue for
                Dim lTrimmed As String = TextLines(i).TrimStart()
                If lTrimmed.StartsWith("'''") Then
                    lStartLine = i
                Else
                    Exit For
                End If
            Next
            Return lStartLine
        End Function

        ''' <summary>
        ''' Handles the text-area context menu's "Go to Line..." item
        ''' </summary>
        Private Sub OnContextMenuGoToLine(vSender As Object, vArgs As EventArgs)
            Try
                ' Use the existing Go To Line dialog from MainWindow
                ' We need to raise an event or call a delegate since we don't have direct access to MainWindow
                RaiseEvent GoToLineRequested()

            Catch ex As Exception
                Console.WriteLine($"OnContextMenuGoToLine error: {ex.Message}")
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

        ''' <summary>
        ''' Finds the most specific (deepest) declaration node - method, property,
        ''' class/module/interface/structure/enum, or namespace - that contains vLine,
        ''' preferring a nested member over its containing type
        ''' </summary>
        Private Function FindContainingBlockNode(vNode As SyntaxNode, vLine As Integer) As SyntaxNode
            If vNode Is Nothing Then Return Nothing
            Try
                ' Check children first so a nested member wins over its containing type
                If vNode.Children IsNot Nothing Then
                    for each lChild As SyntaxNode in vNode.Children
                        Dim lResult As SyntaxNode = FindContainingBlockNode(lChild, vLine)
                        If lResult IsNot Nothing Then Return lResult
                    Next
                End If

                Select Case vNode.NodeType
                    Case CodeNodeType.eMethod, CodeNodeType.eFunction, CodeNodeType.eConstructor,
                         CodeNodeType.eProperty, CodeNodeType.eClass, CodeNodeType.eModule,
                         CodeNodeType.eInterface, CodeNodeType.eStructure, CodeNodeType.eEnum,
                         CodeNodeType.eNamespace
                        If vNode.StartLine <= vLine AndAlso vNode.EndLine >= vLine Then
                            Return vNode
                        End If
                End Select

            Catch ex As Exception
                Console.WriteLine($"FindContainingBlockNode error: {ex.Message}")
            End Try
            Return Nothing
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

    ''' <summary>
    ''' Event arguments for Go to Definition requests
    ''' </summary>
    ''' <remarks>
    ''' Contains information about the word and location where Go to Definition was requested
    ''' </remarks>
    Public Class GotoDefinitionEventArgs
        Inherits EventArgs

        ''' <summary>
        ''' Gets or sets the file path where the request originated
        ''' </summary>
        ''' <value>Full path to the source file</value>
        Public Property FilePath As String

        ''' <summary>
        ''' Gets or sets the line number where the word is located (0-based)
        ''' </summary>
        ''' <value>0-based line index</value>
        Public Property LineNumber As Integer

        ''' <summary>
        ''' Gets or sets the column position where the word starts (0-based)
        ''' </summary>
        ''' <value>0-based column index</value>
        Public Property ColumnNumber As Integer

        ''' <summary>
        ''' Gets or sets the word/symbol for which definition is requested
        ''' </summary>
        ''' <value>The text of the symbol to find</value>
        Public Property Word As String

    End Class

End Namespace
