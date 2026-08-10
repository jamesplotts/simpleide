' Widgets/ResxEditor.vb - Grid-based .resx string-resource editor, opened as a notebook
' tab. Deliberately does NOT implement IEditor, following the same pattern as
' ReferenceManagerTab/PreferencesTab/HelpBrowser - a settings/data view rather than a
' text editor. Because of that, it also deliberately keeps its own TabInfo.Modified
' always False and handles saving entirely through its own Save button rather than
' Ctrl+S/tab-close prompts - MainWindow's generic Save/CheckForUnsavedChanges path calls
' TabInfo.Editor.SaveContent() unconditionally when Modified is True, which would throw
' for any special tab (Editor Is Nothing) including this one
Imports Gtk
Imports System
Imports System.IO
Imports System.Xml
Imports System.Collections.Generic
Imports SimpleIDE.Managers
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities

Namespace Widgets

    ''' <summary>
    ''' Grid editor for a .resx file's string resources (Name/Value/Comment). Resources
    ''' with a "type" or "mimetype" attribute (icons, images, and other non-string
    ''' resources) are left untouched on disk rather than shown/edited here - this editor
    ''' only handles the plain-string case
    ''' </summary>
    Public Class ResxEditor
        Inherits Box

        ''' <summary>One row's worth of resx data - kept as the row's Tag so edits mutate
        ''' the same instance the grid is displaying</summary>
        Private Class ResxEntry
            Public Property Name As String
            Public Property Value As String
            Public Property Comment As String
        End Class

        ' ===== Private Fields =====
        Private pFilePath As String
        Private pThemeManager As ThemeManager
        Private pGrid As CustomDrawDataGrid
        Private pEntries As New List(Of ResxEntry)
        Private pXmlDoc As XmlDocument
        Private pAddButton As CustomDrawButton
        Private pRemoveButton As CustomDrawButton
        Private pSaveButton As CustomDrawButton
        Private pStatusLabel As Label
        Private pIsModified As Boolean = False

        ' ===== Constructor =====

        Public Sub New(vFilePath As String, Optional vThemeManager As ThemeManager = Nothing)
            MyBase.New(Orientation.Vertical, 0)
            pFilePath = vFilePath
            pThemeManager = vThemeManager

            Try
                BuildUI()
                LoadResxFile()
                ShowAll()

            Catch ex As Exception
                Console.WriteLine($"ResxEditor constructor error: {ex.Message}")
            End Try
        End Sub

        ' ===== UI Construction =====

        Private Sub BuildUI()
            Try
                Dim lToolbar As New Box(Orientation.Horizontal, 6)
                lToolbar.BorderWidth = 6

                pAddButton = New CustomDrawButton("Add")
                pAddButton.ThemeManager = pThemeManager
                AddHandler pAddButton.Clicked, AddressOf OnAddEntry
                lToolbar.PackStart(pAddButton, False, False, 0)

                pRemoveButton = New CustomDrawButton("Remove")
                pRemoveButton.ThemeManager = pThemeManager
                AddHandler pRemoveButton.Clicked, AddressOf OnRemoveEntry
                lToolbar.PackStart(pRemoveButton, False, False, 0)

                pSaveButton = New CustomDrawButton("Save")
                pSaveButton.ThemeManager = pThemeManager
                AddHandler pSaveButton.Clicked, AddressOf OnSave
                lToolbar.PackStart(pSaveButton, False, False, 0)

                pStatusLabel = New Label("")
                pStatusLabel.Halign = Align.Start
                lToolbar.PackStart(pStatusLabel, True, True, 6)

                PackStart(lToolbar, False, False, 0)

                pGrid = CreateGrid()
                PackStart(pGrid, True, True, 0)

                AddHandler pGrid.CellDoubleClicked, AddressOf OnCellDoubleClicked

            Catch ex As Exception
                Console.WriteLine($"ResxEditor.BuildUI error: {ex.Message}")
            End Try
        End Sub

        Private Function CreateGrid() As CustomDrawDataGrid
            Dim lGrid As New CustomDrawDataGrid()
            Try
                lGrid.Columns.Add(New DataGridColumn() With {
                    .Name = "Name", .Title = "Name", .Width = 220, .MinWidth = 80,
                    .Resizable = True, .Sortable = True, .DataType = DataGridColumnType.eText
                })
                lGrid.Columns.Add(New DataGridColumn() With {
                    .Name = "Value", .Title = "Value", .Width = 280, .MinWidth = 80,
                    .Resizable = True, .Sortable = True, .DataType = DataGridColumnType.eText,
                    .Ellipsize = True, .AutoExpand = True
                })
                lGrid.Columns.Add(New DataGridColumn() With {
                    .Name = "Comment", .Title = "Comment", .Width = 220, .MinWidth = 60,
                    .Resizable = True, .Sortable = True, .DataType = DataGridColumnType.eText
                })

                lGrid.ShowGridLines = True
                lGrid.AlternateRowColors = True
                lGrid.AllowColumnResize = True
                lGrid.AllowSort = True
                lGrid.MultiSelectEnabled = False

                If pThemeManager IsNot Nothing Then lGrid.SetThemeManager(pThemeManager)

            Catch ex As Exception
                Console.WriteLine($"ResxEditor.CreateGrid error: {ex.Message}")
            End Try
            Return lGrid
        End Function

        ' ===== Load / Save =====

        ''' <summary>
        ''' Loads pFilePath (or the standard empty-resx template if it doesn't exist yet)
        ''' into pXmlDoc, and populates pEntries/the grid from every string-typed &lt;data&gt;
        ''' element found
        ''' </summary>
        Private Sub LoadResxFile()
            Try
                pEntries.Clear()
                pXmlDoc = New XmlDocument()

                If File.Exists(pFilePath) Then
                    pXmlDoc.Load(pFilePath)
                Else
                    pXmlDoc.LoadXml(StringResources.Instance.GetString(StringResources.KEY_RESX_TEMPLATE))
                End If

                Dim lDataNodes As XmlNodeList = pXmlDoc.SelectNodes("//data")
                If lDataNodes IsNot Nothing Then
                    for each lNode As XmlNode in lDataNodes
                        ' Non-string resources (icons, images, etc.) carry a type/mimetype
                        ' attribute - leave those alone, don't show them here
                        If lNode.Attributes("type") IsNot Nothing OrElse lNode.Attributes("mimetype") IsNot Nothing Then
                            Continue for
                        End If

                        Dim lNameAttr As XmlAttribute = lNode.Attributes("name")
                        If lNameAttr Is Nothing Then Continue for

                        Dim lValueNode As XmlNode = lNode.SelectSingleNode("value")
                        Dim lCommentNode As XmlNode = lNode.SelectSingleNode("comment")

                        pEntries.Add(New ResxEntry With {
                            .Name = lNameAttr.Value,
                            .Value = If(lValueNode?.InnerText, ""),
                            .Comment = If(lCommentNode?.InnerText, "")
                        })
                    Next
                End If

                PopulateGrid()
                pIsModified = False
                UpdateStatus()

            Catch ex As Exception
                Console.WriteLine($"ResxEditor.LoadResxFile error: {ex.Message}")
                pStatusLabel.Text = $"Failed to load: {ex.Message}"
            End Try
        End Sub

        Private Sub PopulateGrid()
            Try
                pGrid.ClearRows()

                for each lEntry in pEntries
                    Dim lRow As New DataGridRow()
                    lRow.Tag = lEntry
                    lRow.Cells.Add(New DataGridCell(lEntry.Name))
                    lRow.Cells.Add(New DataGridCell(lEntry.Value))
                    lRow.Cells.Add(New DataGridCell(lEntry.Comment))
                    pGrid.AddRow(lRow)
                Next

            Catch ex As Exception
                Console.WriteLine($"ResxEditor.PopulateGrid error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Writes pEntries back into pXmlDoc (replacing only the string-resource &lt;data&gt;
        ''' elements this editor manages - any binary/typed ones loaded alongside them are
        ''' left in place untouched) and saves to pFilePath
        ''' </summary>
        Private Sub SaveResxFile()
            Dim lRoot As XmlElement = pXmlDoc.DocumentElement
            If lRoot Is Nothing Then Return

            Dim lExisting As New List(Of XmlNode)
            Dim lDataNodes As XmlNodeList = lRoot.SelectNodes("data")
            If lDataNodes IsNot Nothing Then
                for each lNode As XmlNode in lDataNodes
                    If lNode.Attributes("type") Is Nothing AndAlso lNode.Attributes("mimetype") Is Nothing Then
                        lExisting.Add(lNode)
                    End If
                Next
            End If
            for each lNode in lExisting
                lRoot.RemoveChild(lNode)
            Next

            for each lEntry in pEntries
                Dim lDataElement As XmlElement = pXmlDoc.CreateElement("data")
                lDataElement.SetAttribute("name", lEntry.Name)
                lDataElement.SetAttribute("xml:space", "preserve")

                Dim lValueElement As XmlElement = pXmlDoc.CreateElement("value")
                lValueElement.InnerText = lEntry.Value
                lDataElement.AppendChild(lValueElement)

                If Not String.IsNullOrEmpty(lEntry.Comment) Then
                    Dim lCommentElement As XmlElement = pXmlDoc.CreateElement("comment")
                    lCommentElement.InnerText = lEntry.Comment
                    lDataElement.AppendChild(lCommentElement)
                End If

                lRoot.AppendChild(lDataElement)
            Next

            Dim lSettings As New XmlWriterSettings() With {
                .Indent = True,
                .IndentChars = "  ",
                .Encoding = New System.Text.UTF8Encoding(False)
            }
            Using lWriter As XmlWriter = XmlWriter.Create(pFilePath, lSettings)
                pXmlDoc.Save(lWriter)
            End Using

            pIsModified = False
            UpdateStatus()
        End Sub

        Private Sub MarkModified()
            pIsModified = True
            UpdateStatus()
        End Sub

        Private Sub UpdateStatus()
            Dim lCount As String = $"{pEntries.Count} resource(s)"
            pStatusLabel.Text = If(pIsModified, $"{lCount} - unsaved changes", lCount)
            pSaveButton.Sensitive = pIsModified
        End Sub

        ' ===== Event Handlers =====

        Private Sub OnAddEntry(vSender As Object, vArgs As EventArgs)
            Try
                Using lInput As New InputDialog(GetTopLevelWindow(), "Add Resource", "Resource name:", "", pThemeManager)
                    If lInput.Run() <> CInt(ResponseType.Ok) Then Return

                    Dim lName As String = lInput.Text.Trim()
                    If String.IsNullOrEmpty(lName) Then Return

                    If pEntries.any(Function(e) e.Name = lName) Then
                        ShowError($"'{lName}' already exists")
                        Return
                    End If

                    pEntries.Add(New ResxEntry With {.Name = lName, .Value = "", .Comment = ""})
                    PopulateGrid()
                    MarkModified()
                End Using

            Catch ex As Exception
                Console.WriteLine($"ResxEditor.OnAddEntry error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnRemoveEntry(vSender As Object, vArgs As EventArgs)
            Try
                Dim lSelected As List(Of DataGridRow) = pGrid.GetSelectedRows()
                If lSelected.Count = 0 Then Return

                Dim lEntry As ResxEntry = TryCast(lSelected(0).Tag, ResxEntry)
                If lEntry Is Nothing Then Return

                pEntries.Remove(lEntry)
                PopulateGrid()
                MarkModified()

            Catch ex As Exception
                Console.WriteLine($"ResxEditor.OnRemoveEntry error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' There's no in-place cell editor built for CustomDrawDataGrid anywhere in this
        ''' codebase yet, so editing reuses the existing InputDialog prompt (same one
        ''' Rename Symbol/Add Branch use) rather than building brand-new overlay-positioning
        ''' UI just for this one grid
        ''' </summary>
        Private Sub OnCellDoubleClicked(vRowIndex As Integer, vColumnIndex As Integer, vValue As Object)
            Try
                If vRowIndex < 0 OrElse vRowIndex >= pGrid.Rows.Count Then Return
                If vColumnIndex < 0 OrElse vColumnIndex >= pGrid.Columns.Count Then Return

                Dim lEntry As ResxEntry = TryCast(pGrid.Rows(vRowIndex).Tag, ResxEntry)
                If lEntry Is Nothing Then Return

                Dim lColumnTitle As String = pGrid.Columns(vColumnIndex).Title
                Dim lCurrentValue As String = If(vValue?.ToString(), "")

                Using lInput As New InputDialog(GetTopLevelWindow(), $"Edit {lColumnTitle}", $"{lColumnTitle}:", lCurrentValue, pThemeManager)
                    If lInput.Run() <> CInt(ResponseType.Ok) Then Return

                    Dim lNewValue As String = lInput.Text
                    If lNewValue = lCurrentValue Then Return

                    Select Case vColumnIndex
                        Case 0
                            Dim lTrimmed As String = lNewValue.Trim()
                            If String.IsNullOrEmpty(lTrimmed) Then
                                ShowError("Name cannot be empty")
                                Return
                            End If
                            If lTrimmed <> lEntry.Name AndAlso pEntries.any(Function(e) e.Name = lTrimmed) Then
                                ShowError($"'{lTrimmed}' already exists")
                                Return
                            End If
                            lEntry.Name = lTrimmed
                            lNewValue = lTrimmed
                        Case 1
                            lEntry.Value = lNewValue
                        Case 2
                            lEntry.Comment = lNewValue
                    End Select

                    pGrid.UpdateCell(vRowIndex, vColumnIndex, lNewValue)
                    MarkModified()
                End Using

            Catch ex As Exception
                Console.WriteLine($"ResxEditor.OnCellDoubleClicked error: {ex.Message}")
            End Try
        End Sub

        Private Sub OnSave(vSender As Object, vArgs As EventArgs)
            Try
                SaveResxFile()

            Catch ex As Exception
                Console.WriteLine($"ResxEditor.OnSave error: {ex.Message}")
                ShowError($"Failed to save: {ex.Message}")
            End Try
        End Sub

        ' ===== Helpers =====

        Private Function GetTopLevelWindow() As Window
            Try
                Dim lParent As Widget = Me.Parent
                While lParent IsNot Nothing
                    If TypeOf lParent Is Window Then
                        Return DirectCast(lParent, Window)
                    End If
                    lParent = lParent.Parent
                End While
            Catch ex As Exception
                Console.WriteLine($"GetTopLevelWindow error: {ex.Message}")
            End Try
            Return Nothing
        End Function

        Private Sub ShowError(vMessage As String)
            Dim lDialog As New MessageDialog(
                GetTopLevelWindow(),
                DialogFlags.Modal,
                MessageType.error,
                ButtonsType.Ok,
                vMessage
            )
            lDialog.Run()
            lDialog.Destroy()
        End Sub

    End Class

End Namespace
