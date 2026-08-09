' Widgets/CustomDrawProjectExplorer.ContextMenu.vb - Context menu implementation
' Created: 2025-08-17
Imports Gtk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports SimpleIDE.Models
Imports SimpleIDE.Utilities
Imports SimpleIDE.Managers

Namespace Widgets
    
    ''' <summary>
    ''' Partial class containing context menu functionality
    ''' </summary>
    Partial Public Class CustomDrawProjectExplorer
        Inherits Box
        
        ' ===== Context Menu Initialization =====
        
        ''' <summary>
        ''' Initializes the context menu
        ''' </summary>
        Private Sub InitializeContextMenu()
            Try
                pContextMenu = New Menu()
                
                ' Add New Item submenu
                CreateAddNewItemMenu()
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Cut
                Dim lCutItem As New MenuItem("Cut")
                AddHandler lCutItem.Activated, AddressOf OnContextMenuCut
                pContextMenu.Append(lCutItem)
                
                ' Copy
                Dim lCopyItem As New MenuItem("Copy")
                AddHandler lCopyItem.Activated, AddressOf OnContextMenuCopy
                pContextMenu.Append(lCopyItem)
                
                ' Paste
                Dim lPasteItem As New MenuItem("Paste")
                AddHandler lPasteItem.Activated, AddressOf OnContextMenuPaste
                pContextMenu.Append(lPasteItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Delete
                Dim lDeleteItem As New MenuItem("Delete")
                AddHandler lDeleteItem.Activated, AddressOf OnContextMenuDelete
                pContextMenu.Append(lDeleteItem)
                
                ' Rename
                Dim lRenameItem As New MenuItem("Rename...")
                AddHandler lRenameItem.Activated, AddressOf OnContextMenuRename
                pContextMenu.Append(lRenameItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Exclude From Project
                Dim lExcludeItem As New MenuItem("Exclude From Project")
                AddHandler lExcludeItem.Activated, AddressOf OnContextMenuExclude
                pContextMenu.Append(lExcludeItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Open in File Manager
                Dim lOpenFolderItem As New MenuItem("Open in File Manager")
                AddHandler lOpenFolderItem.Activated, AddressOf OnContextMenuOpenInFileManager
                pContextMenu.Append(lOpenFolderItem)
                
                ' Open in Terminal
                Dim lOpenTerminalItem As New MenuItem("Open in Terminal")
                AddHandler lOpenTerminalItem.Activated, AddressOf OnContextMenuOpenInTerminal
                pContextMenu.Append(lOpenTerminalItem)
                
                pContextMenu.Append(New SeparatorMenuItem())
                
                ' Properties
                Dim lPropertiesItem As New MenuItem("Properties")
                AddHandler lPropertiesItem.Activated, AddressOf OnContextMenuProperties
                pContextMenu.Append(lPropertiesItem)

                ' Solution Settings - only ever shown for the synthetic solution root node
                ' (ProjectNodeType.eSolution), toggled in CustomizeContextMenu. The separator
                ' is toggled alongside it so there's no dangling separator with nothing after
                ' it when the item is hidden for every other node type.
                pSolutionSettingsSeparator = New SeparatorMenuItem()
                pContextMenu.Append(pSolutionSettingsSeparator)

                pSolutionSettingsItem = New MenuItem("Solution Settings...")
                AddHandler pSolutionSettingsItem.Activated, AddressOf OnContextMenuSolutionSettings
                pContextMenu.Append(pSolutionSettingsItem)

                pContextMenu.ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"InitializeContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates the Add New Item submenu with Empty VB File option
        ''' </summary>
        Private Sub CreateAddNewItemMenu()
            Try
                Dim lAddMenuItem As New MenuItem("Add")
                Dim lAddSubmenu As New Menu()
                
                ' New Item
                Dim lNewItemItem As New MenuItem("New Item...")
                AddHandler lNewItemItem.Activated, AddressOf OnContextMenuAddNewItem
                lAddSubmenu.Append(lNewItemItem)
                
                ' Existing Item
                Dim lExistingItem As New MenuItem("Existing Item...")
                AddHandler lExistingItem.Activated, AddressOf OnContextMenuAddExistingItem
                lAddSubmenu.Append(lExistingItem)
                
                lAddSubmenu.Append(New SeparatorMenuItem())
                
                ' Empty VB File - ADD THIS NEW OPTION
                Dim lEmptyVBFileItem As New MenuItem("Empty VB File...")
                AddHandler lEmptyVBFileItem.Activated, AddressOf OnContextMenuAddEmptyVBFile
                lAddSubmenu.Append(lEmptyVBFileItem)
                
                lAddSubmenu.Append(New SeparatorMenuItem())
                
                ' New Folder
                Dim lNewFolderItem As New MenuItem("New Folder")
                AddHandler lNewFolderItem.Activated, AddressOf OnContextMenuAddNewFolder
                lAddSubmenu.Append(lNewFolderItem)
                
                lAddSubmenu.Append(New SeparatorMenuItem())
                
                ' Class
                Dim lClassItem As New MenuItem("Class...")
                AddHandler lClassItem.Activated, AddressOf OnContextMenuAddClass
                lAddSubmenu.Append(lClassItem)
                
                ' Module
                Dim lModuleItem As New MenuItem("Module...")
                AddHandler lModuleItem.Activated, AddressOf OnContextMenuAddModule
                lAddSubmenu.Append(lModuleItem)
                
                ' Interface
                Dim lInterfaceItem As New MenuItem("Interface...")
                AddHandler lInterfaceItem.Activated, AddressOf OnContextMenuAddInterface
                lAddSubmenu.Append(lInterfaceItem)
                
                ' Form
                Dim lFormItem As New MenuItem("Windows Form...")
                AddHandler lFormItem.Activated, AddressOf OnContextMenuAddForm
                lAddSubmenu.Append(lFormItem)
                
                ' User Control
                Dim lUserControlItem As New MenuItem("User Control...")
                AddHandler lUserControlItem.Activated, AddressOf OnContextMenuAddUserControl
                lAddSubmenu.Append(lUserControlItem)
                
                lAddMenuItem.Submenu = lAddSubmenu
                pContextMenu.Append(lAddMenuItem)
                
            Catch ex As Exception
                Console.WriteLine($"CreateAddNewItemMenu error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Shows the context menu at the current mouse position
        ''' </summary>
        Private Sub ShowContextMenu(vEvent As Gdk.Event)
            Try
                ' When a solution is loaded, every context-menu action below (Add File, New
                ' Folder, Delete, Rename, etc. - AddNewItem.vb/ContextMenu.vb) reads pProjectManager
                ' directly rather than resolving the clicked node's own project each time - point
                ' it at whichever project actually owns the just-selected node (SelectNode already
                ' ran in OnButtonPress before this), so those existing handlers act on the right
                ' project without each needing its own resolution logic. A no-op when no solution
                ' is loaded (GetOwningProjectManager falls back to pProjectManager itself) or when
                ' the selected node has no resolvable owner (e.g. nothing selected)
                pProjectManager = GetOwningProjectManager(pSelectedNode?.Node)

                ' Customize menu based on selection
                CustomizeContextMenu()
                
                ' Show the menu
                pContextMenu.ShowAll()
                pContextMenu.PopupAtPointer(vEvent)
                
            Catch ex As Exception
                Console.WriteLine($"ShowContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Customizes context menu items based on current selection
        ''' </summary>
        Private Sub CustomizeContextMenu()
            Try
                If pSelectedNode Is Nothing OrElse pSelectedNode.Node Is Nothing Then
                    ' Disable most items if nothing selected
                    SetMenuSensitivity(False)
                    pSolutionSettingsSeparator.Visible = False
                    pSolutionSettingsItem.Visible = False
                    Return
                End If

                SetMenuSensitivity(True)

                ' Customize based on node type
                Dim lIsFile As Boolean = pSelectedNode.Node.IsFile
                Dim lIsProject As Boolean = pSelectedNode.Node.NodeType = ProjectNodeType.eProject
                Dim lIsSolution As Boolean = pSelectedNode.Node.NodeType = ProjectNodeType.eSolution
                Dim lIsSpecial As Boolean = pSelectedNode.Node.NodeType = ProjectNodeType.eReferences OrElse
                                           pSelectedNode.Node.NodeType = ProjectNodeType.eResources OrElse
                                           pSelectedNode.Node.NodeType = ProjectNodeType.eManifest

                ' Can't delete or rename project node, the solution root, or special nodes
                If lIsProject OrElse lIsSolution OrElse lIsSpecial Then
                    SetMenuItemSensitivity("Delete", False)
                    SetMenuItemSensitivity("Rename...", False)
                    SetMenuItemSensitivity("Exclude From Project", False)
                End If

                ' Add items only for folders
                If lIsFile Then
                    SetMenuItemSensitivity("Add", False)
                End If

                ' Solution Settings only makes sense for the solution root
                pSolutionSettingsSeparator.Visible = lIsSolution
                pSolutionSettingsItem.Visible = lIsSolution
                
            Catch ex As Exception
                Console.WriteLine($"CustomizeContextMenu error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets sensitivity for all menu items
        ''' </summary>
        Private Sub SetMenuSensitivity(vSensitive As Boolean)
            Try
                for each lChild As Widget in pContextMenu.Children
                    If TypeOf lChild Is MenuItem Then
                        lChild.Sensitive = vSensitive
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SetMenuSensitivity error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Sets sensitivity for a specific menu item
        ''' </summary>
        Private Sub SetMenuItemSensitivity(vLabel As String, vSensitive As Boolean)
            Try
                for each lChild As Widget in pContextMenu.Children
                    If TypeOf lChild Is MenuItem Then
                        Dim lMenuItem As MenuItem = DirectCast(lChild, MenuItem)
                        If lMenuItem.Label = vLabel Then
                            lMenuItem.Sensitive = vSensitive
                            Exit for
                        End If
                    End If
                Next
                
            Catch ex As Exception
                Console.WriteLine($"SetMenuItemSensitivity error: {ex.Message}")
            End Try
        End Sub
        
        ' ===== Context Menu Event Handlers =====
        
        Private Sub OnContextMenuCut(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement cut functionality
                #If DEBUG Then
                Console.WriteLine("Cut not yet implemented")
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuCut error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuCopy(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement copy functionality
                #If DEBUG Then
                Console.WriteLine("Copy not yet implemented")
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuCopy error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuPaste(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement paste functionality
                #If DEBUG Then
                Console.WriteLine("Paste not yet implemented")
                #End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuPaste error: {ex.Message}")
            End Try
        End Sub
        

        ''' <summary>
        ''' Handles the Delete context menu item activation
        ''' </summary>
        ''' <param name="vSender">The menu item that raised the event</param>
        ''' <param name="vArgs">Event arguments (unused)</param>
        ''' <remarks>
        ''' This method handles both file and directory deletion. It:
        ''' - Removes files from the project file (.vbproj) via ProjectManager
        ''' - Deletes the physical files/directories from disk
        ''' - Updates the Project Explorer display
        ''' - For directories, recursively removes all contained .vb files from the project
        ''' </remarks>
        Private Sub OnContextMenuDelete(vSender As Object, vArgs As EventArgs)
            Try
                If pSelectedNode?.Node Is Nothing Then Return
                
                Dim lPath As String = pSelectedNode.Node.Path
                If String.IsNullOrEmpty(lPath) Then Return
                
                ' Confirm deletion
                Dim lDialog As New MessageDialog(
                    Me.Toplevel,
                    DialogFlags.Modal,
                    MessageType.Question,
                    ButtonsType.YesNo,
                    $"Are you sure you want to delete '{pSelectedNode.Node.Name}'?")
                
                If lDialog.Run() = CInt(ResponseType.Yes) Then
                    Dim lSuccess As Boolean = False
                    
                    If pSelectedNode.Node.IsFile Then
                        ' CRITICAL: First remove from project file via ProjectManager
                        If pProjectManager IsNot Nothing Then
                            ' Remove from project file and internal structures
                            If pProjectManager.RemoveFileFromProject(lPath) Then
                                #If DEBUG Then
                                Console.WriteLine($"Removed {lPath} from project file")
                                #End If
                            Else
                                #If DEBUG Then
                                Console.WriteLine($"Warning: Could Not remove {lPath} from project file")
                                #End If
                            End If
                        End If
                        
                        ' Then delete the physical file
                        If File.Exists(lPath) Then
                            File.Delete(lPath)
                            #If DEBUG Then
                            Console.WriteLine($"Deleted physical file: {lPath}")
                            #End If
                            lSuccess = True
                        End If
                    Else
                        ' For directories, we need to handle all contained files
                        If Directory.Exists(lPath) Then
                            ' Get all .vb files in the directory recursively
                            Dim lVbFiles As List(Of String) = ProjectFileScanner.GetVBFiles(lPath)
                            
                            ' Remove each file from the project
                            If pProjectManager IsNot Nothing Then
                                For Each lFile As String In lVbFiles
                                    If pProjectManager.RemoveFileFromProject(lFile) Then
                                        #If DEBUG Then
                                        Console.WriteLine($"Removed {lFile} from project file")
                                        #End If
                                    End If
                                Next
                            End If
                            
                            ' Delete the physical directory
                            Directory.Delete(lPath, True)
                            #If DEBUG Then
                            Console.WriteLine($"Deleted directory: {lPath}")
                            #End If
                            lSuccess = True
                        End If
                    End If
                    
                    If lSuccess Then
                        ' Refresh project explorer - RefreshProject() is solution-aware (routes
                        ' to LoadSolutionFromManager when a .sln is loaded); calling
                        ' LoadProjectFromManager directly here would silently collapse a
                        ' multi-project solution tree down to just the startup project
                        RefreshProject()

                        ' Raise the project modified event
                        RaiseEvent ProjectModified()
                    Else
                        ShowErrorDialog($"'{pSelectedNode.Node.Name}' Could Not be deleted - it was Not found on disk.")
                    End If
                End If

                lDialog.Destroy()

            Catch ex As Exception
                Console.WriteLine($"OnContextMenuDelete error: {ex.Message}")
                ShowErrorDialog($"Failed To delete: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuExclude(vSender As Object, vArgs As EventArgs)
            Try
                ' TODO: Implement exclude from project
                ' Not raising ProjectModified here - this handler doesn't actually change
                ' anything yet, so it shouldn't mark the project dirty (that previously made
                ' the title bar/tab show unsaved changes for an action that did nothing)
                #If DEBUG Then
                Console.WriteLine("Exclude from project Not yet implemented")
                #End If

            Catch ex As Exception
                Console.WriteLine($"OnContextMenuExclude error: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnContextMenuOpenInFileManager(vSender As Object, vArgs As EventArgs)
            Try
                If pSelectedNode?.Node Is Nothing Then Return
                
                Dim lPath As String = If(pSelectedNode.Node.IsFile,
                                        System.IO.Path.GetDirectoryName(pSelectedNode.Node.Path),
                                        pSelectedNode.Node.Path)
                
                If Directory.Exists(lPath) Then
                    System.Diagnostics.Process.Start("xdg-open", lPath)
                End If
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuOpenInFileManager error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Terminal emulators to try, in order, when opening a directory. Each is launched
        ''' with no arguments and ProcessStartInfo.WorkingDirectory set to the target path -
        ''' this avoids needing to know each emulator's own (differing) working-directory
        ''' flag syntax. Previously hardcoded to "gnome-terminal" alone, which threw
        ''' (Win32Exception, "No such file or directory") and failed silently on any desktop
        ''' that doesn't ship it, e.g. a plain KDE/KWin install with only konsole.
        ''' </summary>
        Private Shared ReadOnly kTerminalEmulators() As String = {"konsole", "gnome-terminal", "xfce4-terminal", "xterm"}

        Private Sub OnContextMenuOpenInTerminal(vSender As Object, vArgs As EventArgs)
            Try
                If pSelectedNode?.Node Is Nothing Then Return

                Dim lPath As String = If(pSelectedNode.Node.IsFile,
                                        System.IO.Path.GetDirectoryName(pSelectedNode.Node.Path),
                                        pSelectedNode.Node.Path)

                If Not Directory.Exists(lPath) Then Return

                For Each lTerminal In kTerminalEmulators
                    Try
                        Dim lStartInfo As New System.Diagnostics.ProcessStartInfo(lTerminal) With {
                            .WorkingDirectory = lPath,
                            .UseShellExecute = False
                        }
                        System.Diagnostics.Process.Start(lStartInfo)
                        Return
                    Catch
                        ' Try the next terminal emulator
                    End Try
                Next

                ShowErrorDialog($"Could not find a terminal emulator to launch. Tried: {String.Join(", ", kTerminalEmulators)}.")

            Catch ex As Exception
                Console.WriteLine($"OnContextMenuOpenInTerminal error: {ex.Message}")
                ShowErrorDialog($"Failed to open terminal: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Shows a minimal read-only properties view for the selected node - full path,
        ''' size/dates and a toggleable read-only flag for a file, or path/project-count
        ''' summary for a project/solution root
        ''' </summary>
        Private Sub OnContextMenuProperties(vSender As Object, vArgs As EventArgs)
            Try
                If pSelectedNode?.Node Is Nothing Then Return
                Dim lNode As ProjectNode = pSelectedNode.Node

                Dim lDialog As New Dialog($"{lNode.Name} Properties",
                                        GetTopLevelWindow(),
                                        DialogFlags.Modal Or DialogFlags.DestroyWithParent)
                lDialog.SetDefaultSize(420, -1)
                lDialog.AddButton("Close", CInt(ResponseType.Close))

                Dim lGrid As New Grid()
                lGrid.RowSpacing = 8
                lGrid.ColumnSpacing = 12
                lGrid.BorderWidth = 12
                Dim lRow As Integer = 0

                Dim lReadOnlyCheck As CustomDrawCheckBox = Nothing

                If lNode.NodeType = ProjectNodeType.eProject OrElse lNode.NodeType = ProjectNodeType.eSolution Then
                    AddPropertyRow(lGrid, lRow, "Name:", lNode.Name)
                    AddPropertyRow(lGrid, lRow, "Path:", lNode.Path)
                    AddPropertyRow(lGrid, lRow, "Type:", If(lNode.NodeType = ProjectNodeType.eSolution, "Solution", "Project"))

                    If lNode.NodeType = ProjectNodeType.eSolution AndAlso pSolutionManager IsNot Nothing Then
                        AddPropertyRow(lGrid, lRow, "Projects:", pSolutionManager.AllProjects.Count.ToString())
                    Else
                        Dim lOwningProject As ProjectManager = If(pSolutionManager?.GetProjectManager(lNode.Path), pProjectManager)
                        If lOwningProject IsNot Nothing AndAlso lOwningProject.SourceFiles IsNot Nothing Then
                            AddPropertyRow(lGrid, lRow, "Source files:", lOwningProject.SourceFiles.Count.ToString())
                        End If
                    End If
                Else
                    Dim lIsDirectory As Boolean = Not lNode.IsFile
                    AddPropertyRow(lGrid, lRow, "Name:", lNode.Name)
                    AddPropertyRow(lGrid, lRow, "Path:", lNode.Path)
                    AddPropertyRow(lGrid, lRow, "Type:", If(lIsDirectory, "Folder", DescribeFileType(lNode)))

                    If lIsDirectory Then
                        If Directory.Exists(lNode.Path) Then
                            Dim lDirInfo As New DirectoryInfo(lNode.Path)
                            AddPropertyRow(lGrid, lRow, "Modified:", lDirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"))
                        End If
                    ElseIf File.Exists(lNode.Path) Then
                        Dim lFileInfo As New FileInfo(lNode.Path)
                        AddPropertyRow(lGrid, lRow, "Size:", FormatFileSize(lFileInfo.Length))
                        AddPropertyRow(lGrid, lRow, "Created:", lFileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"))
                        AddPropertyRow(lGrid, lRow, "Modified:", lFileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"))

                        lReadOnlyCheck = New CustomDrawCheckBox("Read-only")
                        lReadOnlyCheck.ThemeManager = pThemeManager
                        lReadOnlyCheck.Active = lFileInfo.IsReadOnly
                        lGrid.Attach(lReadOnlyCheck, 1, lRow, 1, 1)
                        lRow += 1
                    Else
                        AddPropertyRow(lGrid, lRow, "Status:", "Not found on disk")
                    End If
                End If

                lDialog.ContentArea.PackStart(lGrid, True, True, 0)
                lDialog.ShowAll()
                lDialog.Run()

                ' Apply the read-only toggle (if shown) on close - the checkbox only exists
                ' for a real on-disk file, matching the branch above that created it
                If lReadOnlyCheck IsNot Nothing AndAlso File.Exists(lNode.Path) Then
                    Try
                        Dim lFileInfo As New FileInfo(lNode.Path)
                        If lReadOnlyCheck.Active <> lFileInfo.IsReadOnly Then
                            If lReadOnlyCheck.Active Then
                                File.SetAttributes(lNode.Path, File.GetAttributes(lNode.Path) Or FileAttributes.ReadOnly)
                            Else
                                File.SetAttributes(lNode.Path, File.GetAttributes(lNode.Path) And Not FileAttributes.ReadOnly)
                            End If
                        End If
                    Catch exAttr As Exception
                        Console.WriteLine($"OnContextMenuProperties: Failed to update read-only attribute: {exAttr.Message}")
                    End Try
                End If

                lDialog.Destroy()

            Catch ex As Exception
                Console.WriteLine($"OnContextMenuProperties error: {ex.Message}")
            End Try
        End Sub

        ''' <summary>
        ''' Appends a "Label: value" row to a properties Grid, incrementing vRow
        ''' </summary>
        Private Sub AddPropertyRow(vGrid As Grid, ByRef vRow As Integer, vLabel As String, vValue As String)
            Dim lLabelWidget As New Label(vLabel)
            lLabelWidget.Halign = Align.End
            vGrid.Attach(lLabelWidget, 0, vRow, 1, 1)

            Dim lValueWidget As New Label(vValue)
            lValueWidget.Halign = Align.Start
            lValueWidget.Selectable = True
            lValueWidget.LineWrap = True
            lValueWidget.MaxWidthChars = 40
            vGrid.Attach(lValueWidget, 1, vRow, 1, 1)

            vRow += 1
        End Sub

        ''' <summary>
        ''' Human-readable file-type label derived from the node's own type classification
        ''' </summary>
        Private Function DescribeFileType(vNode As ProjectNode) As String
            Select Case vNode.NodeType
                Case ProjectNodeType.eVBFile : Return "Visual Basic Source File"
                Case ProjectNodeType.eXMLFile : Return "XML File"
                Case ProjectNodeType.eTextFile : Return "Text File"
                Case ProjectNodeType.eConfigFile : Return "Configuration File"
                Case ProjectNodeType.eResourceFile : Return "Resource File"
                Case ProjectNodeType.eImageFile : Return "Image File"
                Case ProjectNodeType.eManifest : Return "Assembly Manifest"
                Case ProjectNodeType.eReference : Return "Reference"
                Case Else : Return "File"
            End Select
        End Function

        ''' <summary>
        ''' Formats a byte count as a human-readable size (B/KB/MB/GB)
        ''' </summary>
        Private Function FormatFileSize(vBytes As Long) As String
            Dim lUnits() As String = {"B", "KB", "MB", "GB"}
            Dim lSize As Double = vBytes
            Dim lUnitIndex As Integer = 0
            While lSize >= 1024 AndAlso lUnitIndex < lUnits.Length - 1
                lSize /= 1024
                lUnitIndex += 1
            End While
            If lUnitIndex = 0 Then Return $"{vBytes:N0} bytes"
            Return $"{lSize:N1} {lUnits(lUnitIndex)} ({vBytes:N0} bytes)"
        End Function

        ''' <summary>
        ''' Handles the "Solution Settings..." context menu item click
        ''' </summary>
        Private Sub OnContextMenuSolutionSettings(vSender As Object, vArgs As EventArgs)
            Try
                RaiseEvent SolutionSettingsRequested()
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuSolutionSettings error: {ex.Message}")
            End Try
        End Sub
        
        ''' <summary>
        ''' Handles the Rename context menu item activation
        ''' </summary>
        ''' <param name="vSender">The menu item that raised the event</param>
        ''' <param name="vArgs">Event arguments (unused)</param>
        ''' <remarks>
        ''' This method handles file renaming by:
        ''' - Updating the project file (.vbproj) to reflect the new name
        ''' - Renaming the physical file on disk
        ''' - The ProjectManager's FileSystemWatcher will detect the rename and fire events
        ''' - Updating the Project Explorer display
        ''' </remarks>
        Private Sub OnContextMenuRename(vSender As Object, vArgs As EventArgs)
            Try
                If pSelectedNode?.Node Is Nothing Then Return
                If Not pSelectedNode.Node.IsFile Then Return
                
                ' Create rename dialog
                Dim lDialog As New Dialog("Rename File",
                                        Me.Toplevel,
                                        DialogFlags.Modal Or DialogFlags.DestroyWithParent)

                lDialog.SetDefaultSize(400, 120)

                ' Create entry with current name (FIXED: using System.IO.Path)
                Dim lEntry As New CustomDrawTextBox()
                Dim lFileName As String = System.IO.Path.GetFileName(pSelectedNode.Node.Path)
                lEntry.Text = lFileName
                lEntry.ThemeManager = pThemeManager
                AddHandler lEntry.Activated, Sub() lDialog.Respond(ResponseType.Ok)

                Dim lBox As New Box(Orientation.Vertical, 6)
                lBox.MarginStart = 12
                lBox.MarginEnd = 12
                lBox.MarginTop = 12
                lBox.MarginBottom = 12

                lBox.PackStart(New Label("New name:"), False, False, 0)
                lBox.PackStart(lEntry, False, False, 0)

                lDialog.ContentArea.PackStart(lBox, True, True, 0)

                Dim lButtonBox As New Box(Orientation.Horizontal, 6)
                lButtonBox.Halign = Align.End
                lButtonBox.BorderWidth = 6
                Dim lCancelButton As New CustomDrawButton("Cancel")
                lCancelButton.ThemeManager = pThemeManager
                AddHandler lCancelButton.Clicked, Sub() lDialog.Respond(ResponseType.Cancel)
                lButtonBox.PackStart(lCancelButton, False, False, 0)
                Dim lRenameButton As New CustomDrawButton("Rename")
                lRenameButton.ThemeManager = pThemeManager
                AddHandler lRenameButton.Clicked, Sub() lDialog.Respond(ResponseType.Ok)
                lButtonBox.PackStart(lRenameButton, False, False, 0)
                Dim lContentBox As Box = TryCast(lDialog.ContentArea, Box)
                If lContentBox IsNot Nothing Then lContentBox.PackStart(lButtonBox, False, False, 0)

                lDialog.ShowAll()
                lEntry.GrabFocus()
                lEntry.InnerEntry.SelectRegion(0, lFileName.LastIndexOf("."c))
                
                If lDialog.Run() = CInt(ResponseType.Ok) Then
                    Dim lNewName As String = lEntry.Text.Trim()
                    
                    If Not String.IsNullOrEmpty(lNewName) AndAlso lNewName <> lFileName Then
                        Try
                            Dim lOldPath As String = pSelectedNode.Node.Path
                            Dim lNewPath As String = System.IO.Path.Combine(
                                System.IO.Path.GetDirectoryName(lOldPath), 
                                lNewName)
                            
                            ' Check if target already exists
                            If File.Exists(lNewPath) Then
                                Dim lOverwriteDialog As New MessageDialog(
                                    Me.Toplevel,
                                    DialogFlags.Modal,
                                    MessageType.Question,
                                    ButtonsType.YesNo,
                                    $"File '{lNewName}' already exists. Overwrite?")
                                
                                Dim lResponse As Integer = lOverwriteDialog.Run()
                                lOverwriteDialog.Destroy()
                                
                                If lResponse <> CInt(ResponseType.Yes) Then
                                    lDialog.Destroy()
                                    Return
                                End If
                            End If
                            
                            ' CRITICAL: Update project file via ProjectManager
                            Dim lProjectUpdated As Boolean = False
                            If pProjectManager IsNot Nothing Then
                                ' First remove the old file from project
                                If pProjectManager.RemoveFileFromProject(lOldPath) Then
                                    ' Then add the new file to project
                                    If pProjectManager.AddFileToProject(lNewPath, "Compile") = True Then
                                        lProjectUpdated = True
                                        #If DEBUG Then
                                        Console.WriteLine($"Updated project file: {lOldPath} -> {lNewPath}")
                                        #End If
                                    End If
                                End If
                            End If
                            
                            ' Rename the physical file
                            File.Move(lOldPath, lNewPath)
                            #If DEBUG Then
                            Console.WriteLine($"Renamed physical file: {lOldPath} -> {lNewPath}")
                            #End If
                            
                            ' The ProjectManager's FileSystemWatcher will detect this rename
                            ' and fire the FileRenamed event automatically (see OnFileRenamed in ProjectManager)
                            
                            ' Refresh project explorer - RefreshProject() is solution-aware (see
                            ' the Delete handler above for why calling LoadProjectFromManager
                            ' directly here would corrupt a multi-project solution tree)
                            RefreshProject()

                            ' Raise project modified event
                            RaiseEvent ProjectModified()
                            
                        Catch ex As Exception
                            Dim lErrorDialog As New MessageDialog(
                                Me.Toplevel,
                                DialogFlags.Modal,
                                MessageType.Error,
                                ButtonsType.Ok,
                                $"Failed To rename file: {ex.Message}")
                            lErrorDialog.Run()
                            lErrorDialog.Destroy()
                        End Try
                    End If
                End If
                
                lDialog.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"OnContextMenuRename error: {ex.Message}")
            End Try
        End Sub
    End Class
    
End Namespace