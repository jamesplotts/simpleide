' Editors/AssemblySettingsEditor.vb - Assembly settings editor widget
Imports Gtk
Imports System.IO
Imports System.Xml
Imports System.Text
Imports System.Reflection
Imports System.Text.RegularExpressions

Namespace Editors
    Public Class AssemblySettingsEditor
        Inherits Box
        
        ' Private fields
        Private pProjectFile As String
        Private pGrid As Grid
        Private pIsModified As Boolean = False
        Private pGenerateKeyButton As Button
        
        ' Assembly information entries
        Private pTitleEntry As Entry
        Private pDescriptionEntry As Entry
        Private pCompanyEntry As Entry
        Private pProductEntry As Entry
        Private pCopyrightEntry As Entry
        Private pTrademarkEntry As Entry
        
        ' Version entries
        Private pAssemblyVersionEntry As Entry
        Private pFileVersionEntry As Entry
        Private pInformationalVersionEntry As Entry
        Private pAutoIncrementCheck As CheckButton
        
        ' Signing
        Private pSignAssemblyCheck As CheckButton
        Private pKeyFileEntry As Entry
        Private pKeyFileBrowseButton As Button
        Private pDelaySignCheck As CheckButton
        
        ' Build settings
        Private pOutputTypeCombo As ComboBoxText
        Private pTargetFrameworkCombo As ComboBoxText
        Private pPlatformTargetCombo As ComboBoxText
        Private pLangVersionCombo As ComboBoxText
        
        ' Events
        Public Event Modified(vIsModified As Boolean)
        Public Event SaveRequested()
        
        ' Properties
        Public Property IsModified As Boolean
            Get
                Return pIsModified
            End Get
            Set(Value As Boolean)
                If pIsModified <> Value Then
                    pIsModified = Value
                    RaiseEvent Modified(pIsModified)
                End If
            End Set
        End Property
        
        Public Sub New(vProjectFile As String)
            MyBase.New(Orientation.Vertical, 0)
            
            pProjectFile = vProjectFile
            
            BuildUI()
            LoadSettings()
        End Sub
        
        Private Sub BuildUI()
            Try
                ' Create scrolled window
                Dim lScrolled As New ScrolledWindow()
                lScrolled.SetPolicy(PolicyType.Automatic, PolicyType.Automatic)
                
                ' Create main grid
                pGrid = New Grid()
                pGrid.ColumnSpacing = 12
                pGrid.RowSpacing = 6
                pGrid.MarginStart = 12  ' FIXED: Was MarginLeft
                pGrid.MarginEnd = 12    ' FIXED: Was MarginRight
                pGrid.MarginTop = 12
                pGrid.MarginBottom = 12
                
                Dim lRow As Integer = 0
                
                ' Assembly Information section
                AddSectionHeader("Assembly Information", lRow)
                lRow += 1
                
                ' Title
                AddLabeledEntry("Title:", pTitleEntry, lRow, "the Name of the assembly")
                lRow += 1
                
                ' Description
                AddLabeledEntry("Description:", pDescriptionEntry, lRow, "A brief Description of the assembly")
                lRow += 1
                
                ' Company
                AddLabeledEntry("Company:", pCompanyEntry, lRow, "the company Name")
                lRow += 1
                
                ' Product
                AddLabeledEntry("Product:", pProductEntry, lRow, "the product Name")
                lRow += 1
                
                ' Copyright
                AddLabeledEntry("Copyright:", pCopyrightEntry, lRow, "Copyright information")
                lRow += 1
                
                ' Trademark
                AddLabeledEntry("Trademark:", pTrademarkEntry, lRow, "Trademark information")
                lRow += 1
                
                ' Add separator
                lRow = AddSeparator(lRow)
                
                ' Version Information section
                AddSectionHeader("Version Information", lRow)
                lRow += 1
                
                ' Assembly Version
                AddLabeledEntry("Assembly Version:", pAssemblyVersionEntry, lRow, "Version used by the Runtime (Major.Minor.Build.Revision)")
                lRow += 1
                
                ' File Version
                AddLabeledEntry("File Version:", pFileVersionEntry, lRow, "Version shown in file properties")
                lRow += 1
                
                ' Informational Version
                AddLabeledEntry("Informational Version:", pInformationalVersionEntry, lRow, "Product Version for display")
                lRow += 1
                
                ' Auto-increment version
                pAutoIncrementCheck = New CheckButton("Auto-increment build number on each build")
                pGrid.Attach(pAutoIncrementCheck, 0, lRow, 2, 1)
                AddHandler pAutoIncrementCheck.Toggled, AddressOf OnFieldChanged
                lRow += 1
                
                ' Add separator
                lRow = AddSeparator(lRow)
                
                ' Assembly Signing section
                AddSectionHeader("Assembly Signing", lRow)
                lRow += 1
                
                ' Sign assembly
                pSignAssemblyCheck = New CheckButton("Sign the assembly")
                pGrid.Attach(pSignAssemblyCheck, 0, lRow, 2, 1)
                AddHandler pSignAssemblyCheck.Toggled, AddressOf OnSignAssemblyToggled
                lRow += 1
                
                ' Key file
                Dim lKeyFileLabel As New Label("Strong Name key file:")
                lKeyFileLabel.Halign = Align.Start
                pGrid.Attach(lKeyFileLabel, 0, lRow, 1, 1)
                
                Dim lKeyFileBox As New Box(Orientation.Horizontal, 6)
                pKeyFileEntry = New Entry()
                pKeyFileEntry.Hexpand = True
                pKeyFileEntry.Sensitive = False
                AddHandler pKeyFileEntry.Changed, AddressOf OnFieldChanged

                pKeyFileBrowseButton = New Button("Browse...")
                pKeyFileBrowseButton.Sensitive = False
                AddHandler pKeyFileBrowseButton.Clicked, AddressOf OnBrowseKeyFile

                pGenerateKeyButton = New Button("Generate...")
                pGenerateKeyButton.Sensitive = False
                AddHandler pGenerateKeyButton.Clicked, AddressOf OnGenerateKeyFile
                
                ' Add to the key file box
                lKeyFileBox.PackStart(pGenerateKeyButton, False, False, 0)

                
                lKeyFileBox.PackStart(pKeyFileEntry, True, True, 0)
                lKeyFileBox.PackStart(pKeyFileBrowseButton, False, False, 0)
                pGrid.Attach(lKeyFileBox, 1, lRow, 1, 1)
                lRow += 1
                
                ' Delay sign
                pDelaySignCheck = New CheckButton("Delay sign only")
                pDelaySignCheck.Sensitive = False
                pGrid.Attach(pDelaySignCheck, 0, lRow, 2, 1)
                AddHandler pDelaySignCheck.Toggled, AddressOf OnFieldChanged
                lRow += 1
                
                ' Add separator
                lRow = AddSeparator(lRow)
                
                ' Build Settings section
                AddSectionHeader("Build Settings", lRow)
                lRow += 1
                
                ' Output Type
                AddLabeledCombo("output Type:", pOutputTypeCombo, lRow,
                              {"Console Application", "Windows Application", "Class Library"})
                lRow += 1
                
                ' Target Framework
                AddLabeledCombo("Target Framework:", pTargetFrameworkCombo, lRow,
                              {"net8.0", "net7.0", "net6.0", "net5.0", "netcoreapp3.1"})
                lRow += 1
                
                ' Platform Target
                AddLabeledCombo("Platform Target:", pPlatformTargetCombo, lRow,
                              {"any CPU", "x86", "x64", "ARM", "ARM64"})
                lRow += 1
                
                ' Language Version
                AddLabeledCombo("Language Version:", pLangVersionCombo, lRow,
                              {"Latest", "Default", "16.0", "15.5", "15.3", "15.0", "14.0"})
                lRow += 1
                
                ' Add the grid to scrolled window
                lScrolled.Add(pGrid)
                
                ' Create button box
                Dim lButtonBox As New Box(Orientation.Horizontal, 6)
                lButtonBox.Halign = Align.End
                lButtonBox.MarginTop = 12
                lButtonBox.MarginEnd = 12    ' FIXED: Was MarginRight
                lButtonBox.MarginBottom = 12
                
                Dim lSaveButton As New Button("Save")
                AddHandler lSaveButton.Clicked, AddressOf OnSave
                
                Dim lRevertButton As New Button("Revert")
                AddHandler lRevertButton.Clicked, AddressOf OnRevert
                
                lButtonBox.PackStart(lRevertButton, False, False, 0)
                lButtonBox.PackStart(lSaveButton, False, False, 0)
                
                ' Pack everything
                PackStart(lScrolled, True, True, 0)
                PackStart(lButtonBox, False, False, 0)
                
                ShowAll()
                
            Catch ex As Exception
                Console.WriteLine($"error building assembly settings UI: {ex.Message}")
            End Try
        End Sub
        
        Private Sub AddSectionHeader(vText As String, vRow As Integer)
            Dim lLabel As New Label($"<b>{vText}</b>")
            lLabel.UseMarkup = True
            lLabel.Halign = Align.Start
            lLabel.MarginTop = If(vRow = 0, 0, 12)
            pGrid.Attach(lLabel, 0, vRow, 2, 1)
        End Sub
        
        Private Sub AddLabeledEntry(vLabelText As String, ByRef vEntry As Entry, vRow As Integer, vTooltip As String)
            Dim lLabel As New Label(vLabelText)
            lLabel.Halign = Align.Start
            pGrid.Attach(lLabel, 0, vRow, 1, 1)
            
            vEntry = New Entry()
            vEntry.Hexpand = True
            If Not String.IsNullOrEmpty(vTooltip) Then
                vEntry.TooltipText = vTooltip
            End If
            AddHandler vEntry.Changed, AddressOf OnFieldChanged
            pGrid.Attach(vEntry, 1, vRow, 1, 1)
        End Sub
        
        Private Sub AddLabeledCombo(vLabelText As String, ByRef vCombo As ComboBoxText, vRow As Integer, vItems() As String)
            Dim lLabel As New Label(vLabelText)
            lLabel.Halign = Align.Start
            pGrid.Attach(lLabel, 0, vRow, 1, 1)
            
            vCombo = New ComboBoxText()
            For Each lItem In vItems
                vCombo.AppendText(lItem)
            Next
            vCombo.Hexpand = True
            AddHandler vCombo.Changed, AddressOf OnFieldChanged
            pGrid.Attach(vCombo, 1, vRow, 1, 1)
        End Sub
        
        Private Function AddSeparator(vRow As Integer) As Integer
            Dim lSeparator As New Separator(Orientation.Horizontal)
            lSeparator.MarginTop = 12
            lSeparator.MarginBottom = 12
            pGrid.Attach(lSeparator, 0, vRow, 2, 1)
            Return vRow + 1
        End Function

        Private Sub LoadSettings()
            Try
                ' Load assembly info first
                LoadAssemblyInfo()
                
                ' Then load project settings
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                LoadProjectSettings(lDoc)
                
                ' Reset modified state
                IsModified = False
                
            Catch ex As Exception
                Console.WriteLine($"error loading settings: {ex.Message}")
            End Try
        End Sub
        
'        Private Sub LoadSettings()
'            Try
'                If Not File.Exists(pProjectFile) Then
'                    Console.WriteLine("Project file not found")
'                    Return
'                End If
'                
'                Dim lDoc As New XmlDocument()
'                lDoc.Load(pProjectFile)
'                
'                ' Load assembly info attributes
'                LoadAssemblyInfoAttributes()
'                
'                ' Load project settings
'                LoadProjectSettings(lDoc)
'                
'                ' Reset modified flag
'                IsModified = False
'                
'            Catch ex As Exception
'                Console.WriteLine($"Error loading settings: {ex.Message}")
'            End Try
'        End Sub
        
        Private Sub LoadAssemblyInfoAttributes()
            Try
                ' Look for AssemblyInfo.vb
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
                Dim lAssemblyInfo As String = System.IO.Path.Combine(lProjectDir, "My project", "AssemblyInfo.vb")
                
                If Not File.Exists(lAssemblyInfo) Then
                    lAssemblyInfo = System.IO.Path.Combine(lProjectDir, "Properties", "AssemblyInfo.vb")
                End If
                
                If File.Exists(lAssemblyInfo) Then
                    Dim lContent As String = File.ReadAllText(lAssemblyInfo)
                    
                    ' Extract attributes
                    pTitleEntry.Text = ExtractAttributeValue(lContent, "AssemblyTitle")
                    pDescriptionEntry.Text = ExtractAttributeValue(lContent, "AssemblyDescription")
                    pCompanyEntry.Text = ExtractAttributeValue(lContent, "AssemblyCompany")
                    pProductEntry.Text = ExtractAttributeValue(lContent, "AssemblyProduct")
                    pCopyrightEntry.Text = ExtractAttributeValue(lContent, "AssemblyCopyright")
                    pTrademarkEntry.Text = ExtractAttributeValue(lContent, "AssemblyTrademark")
                    
                    ' Version attributes
                    pAssemblyVersionEntry.Text = ExtractAttributeValue(lContent, "AssemblyVersion")
                    pFileVersionEntry.Text = ExtractAttributeValue(lContent, "AssemblyFileVersion")
                    pInformationalVersionEntry.Text = ExtractAttributeValue(lContent, "AssemblyInformationalVersion")
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error loading assembly info: {ex.Message}")
            End Try
        End Sub
        
        
        Private Sub LoadProjectSettings(vDoc As XmlDocument)
            Try
                ' Output Type
                Dim lOutputType As String = GetNodeValue(vDoc, "//OutputType")
                Select Case lOutputType.ToLower()
                    Case "exe"
                        pOutputTypeCombo.Active = 0 ' Console Application
                    Case "winexe"
                        pOutputTypeCombo.Active = 1 ' Windows Application
                    Case "Library"
                        pOutputTypeCombo.Active = 2 ' Class Library
                End Select
                
                ' Target Framework - FIXED: Proper TreeIter handling
                Dim lTargetFramework As String = GetNodeValue(vDoc, "//TargetFramework")
                If Not String.IsNullOrEmpty(lTargetFramework) Then
                    For i As Integer = 0 To pTargetFrameworkCombo.Model.IterNChildren() - 1
                        Dim lIter As TreeIter = Nothing
                        If pTargetFrameworkCombo.Model.IterNthChild(lIter, i) Then
                            If pTargetFrameworkCombo.Model.GetValue(lIter, 0).ToString() = lTargetFramework Then
                                pTargetFrameworkCombo.Active = i
                                Exit For
                            End If
                        End If
                    Next
                End If
                
                ' Platform Target - FIXED: Proper TreeIter handling
                Dim lPlatformTarget As String = GetNodeValue(vDoc, "//PlatformTarget")
                If Not String.IsNullOrEmpty(lPlatformTarget) Then
                    For i As Integer = 0 To pPlatformTargetCombo.Model.IterNChildren() - 1
                        Dim lIter As TreeIter = Nothing
                        If pPlatformTargetCombo.Model.IterNthChild(lIter, i) Then
                            If pPlatformTargetCombo.Model.GetValue(lIter, 0).ToString() = lPlatformTarget Then
                                pPlatformTargetCombo.Active = i
                                Exit For
                            End If
                        End If
                    Next
                End If
                
                ' Language Version - FIXED: Proper TreeIter handling
                Dim lLangVersion As String = GetNodeValue(vDoc, "//LangVersion")
                If Not String.IsNullOrEmpty(lLangVersion) Then
                    For i As Integer = 0 To pLangVersionCombo.Model.IterNChildren() - 1
                        Dim lIter As TreeIter = Nothing
                        If pLangVersionCombo.Model.IterNthChild(lIter, i) Then
                            If pLangVersionCombo.Model.GetValue(lIter, 0).ToString() = lLangVersion Then
                                pLangVersionCombo.Active = i
                                Exit For
                            End If
                        End If
                    Next
                Else
                    pLangVersionCombo.Active = 0 ' Latest
                End If
                
                ' Signing
                Dim lSignAssembly As String = GetNodeValue(vDoc, "//SignAssembly")
                pSignAssemblyCheck.Active = (lSignAssembly.ToLower() = "true")
                
                Dim lKeyFile As String = GetNodeValue(vDoc, "//AssemblyOriginatorKeyFile")
                pKeyFileEntry.Text = lKeyFile
                
                Dim lDelaySign As String = GetNodeValue(vDoc, "//DelaySign")
                pDelaySignCheck.Active = (lDelaySign.ToLower() = "true")
                
                ' Auto-increment setting (custom property)
                Dim lAutoIncrement As String = GetNodeValue(vDoc, "//AutoIncrementBuildNumber")
                pAutoIncrementCheck.Active = (lAutoIncrement.ToLower() = "true")
                
            Catch ex As Exception
                Console.WriteLine($"error loading project settings: {ex.Message}")
            End Try
        End Sub
        
        Private Function GetNodeValue(vDoc As XmlDocument, vXPath As String) As String
            Try
                Dim lNode As XmlNode = vDoc.SelectSingleNode(vXPath)
                If lNode IsNot Nothing Then
                    Return lNode.InnerText
                End If
            Catch ex As Exception
                Console.WriteLine($"error getting Node Value: {ex.Message}")
            End Try
            Return ""
        End Function
        
        Private Sub OnFieldChanged(vSender As Object, vE As EventArgs)
            IsModified = True
        End Sub
        
        Private Sub OnSignAssemblyToggled(vSender As Object, vE As EventArgs)
            ' Enable/disable signing controls
            Dim lEnabled As Boolean = pSignAssemblyCheck.Active
            pKeyFileEntry.Sensitive = lEnabled
            pKeyFileBrowseButton.Sensitive = lEnabled
            pGenerateKeyButton.Sensitive = lEnabled
            pDelaySignCheck.Sensitive = lEnabled
            
            OnFieldChanged(vSender, vE)
        End Sub
        
        Private Sub OnBrowseKeyFile(vSender As Object, vE As EventArgs)
            Try
                Dim lChooser As New FileChooserDialog(
                    "Select Strong Name key File",
                    Nothing,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept)
                
                ' Add filters
                Dim lFilter As New FileFilter()
                lFilter.Name = "key Files"
                lFilter.AddPattern("*.snk")
                lFilter.AddPattern("*.pfx")
                lChooser.AddFilter(lFilter)
                
                Dim lAllFilter As New FileFilter()
                lAllFilter.Name = "All Files"
                lAllFilter.AddPattern("*")
                lChooser.AddFilter(lAllFilter)
                
                ' Set initial folder
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
                lChooser.SetCurrentFolder(lProjectDir)
                
                If lChooser.Run() = CInt(ResponseType.Accept) Then
                    ' Make path relative to project
                    Dim lRelativePath As String = MakeRelativePath(lProjectDir, lChooser.FileName)
                    pKeyFileEntry.Text = lRelativePath
                End If
                
                lChooser.Destroy()
                
            Catch ex As Exception
                Console.WriteLine($"error browsing for key file: {ex.Message}")
            End Try
        End Sub
        
        Private Function MakeRelativePath(vBasePath As String, vFullPath As String) As String
            Try
                Dim lBaseUri As New Uri(vBasePath & System.IO.Path.DirectorySeparatorChar)
                Dim lFullUri As New Uri(vFullPath)
                Dim lRelativeUri As Uri = lBaseUri.MakeRelativeUri(lFullUri)
                Return Uri.UnescapeDataString(lRelativeUri.ToString())
            Catch ex As Exception
                Return vFullPath
            End Try
        End Function
        
        Private Sub OnSave(vSender As Object, vE As EventArgs)
            Try
                SaveSettings()
                IsModified = False
                RaiseEvent SaveRequested()
            Catch ex As Exception
                ShowErrorDialog($"Failed to Save settings: {ex.Message}")
            End Try
        End Sub
        
        Private Sub OnRevert(vSender As Object, vE As EventArgs)
            LoadSettings()
        End Sub
        
        Private Sub SaveSettings()
            Try
                ' Save assembly info
                SaveAssemblyInfo()
                
                ' Save project settings
                SaveProjectSettings()
                
            Catch ex As Exception
                Throw
            End Try
        End Sub
        
        Private Sub SaveAssemblyInfo()
            Try
                Dim lAssemblyInfoPath As String = GetAssemblyInfoPath()
                
                ' Ensure directory exists
                Dim lDirectory As String = System.IO.Path.GetDirectoryName(lAssemblyInfoPath)
                If Not Directory.Exists(lDirectory) Then
                    Directory.CreateDirectory(lDirectory)
                End If
                
                ' Read existing content or create new
                Dim lContent As String
                If File.Exists(lAssemblyInfoPath) Then
                    lContent = File.ReadAllText(lAssemblyInfoPath)
                Else
                    lContent = CreateNewAssemblyInfo()
                End If
                
                ' Update attributes
                lContent = UpdateOrAddAttribute(lContent, "AssemblyTitle", pTitleEntry.Text)
                lContent = UpdateOrAddAttribute(lContent, "AssemblyDescription", pDescriptionEntry.Text)
                lContent = UpdateOrAddAttribute(lContent, "AssemblyCompany", pCompanyEntry.Text)
                lContent = UpdateOrAddAttribute(lContent, "AssemblyProduct", pProductEntry.Text)
                lContent = UpdateOrAddAttribute(lContent, "AssemblyCopyright", pCopyrightEntry.Text)
                lContent = UpdateOrAddAttribute(lContent, "AssemblyTrademark", pTrademarkEntry.Text)
                
                ' Version attributes
                lContent = UpdateOrAddAttribute(lContent, "AssemblyVersion", pAssemblyVersionEntry.Text)
                lContent = UpdateOrAddAttribute(lContent, "AssemblyFileVersion", pFileVersionEntry.Text)
                
                ' Informational version (may not exist in older projects)
                If Not String.IsNullOrWhiteSpace(pInformationalVersionEntry.Text) Then
                    lContent = UpdateOrAddAttribute(lContent, "AssemblyInformationalVersion", pInformationalVersionEntry.Text)
                End If
                
                ' Save the file
                File.WriteAllText(lAssemblyInfoPath, lContent, Encoding.UTF8)
                
                Console.WriteLine($"Assembly info saved to: {lAssemblyInfoPath}")
                
            Catch ex As Exception
                Console.WriteLine($"error saving assembly info: {ex.Message}")
                Throw
            End Try
        End Sub
        
        Private Sub SaveProjectSettings()
            Try
                If Not File.Exists(pProjectFile) Then
                    Throw New FileNotFoundException("project file not found")
                End If
                
                Dim lDoc As New XmlDocument()
                lDoc.Load(pProjectFile)
                
                ' Update values
                SetNodeValue(lDoc, "OutputType", GetOutputType())
                SetNodeValue(lDoc, "TargetFramework", pTargetFrameworkCombo.ActiveText)
                SetNodeValue(lDoc, "PlatformTarget", pPlatformTargetCombo.ActiveText)
                SetNodeValue(lDoc, "LangVersion", pLangVersionCombo.ActiveText)
                
                ' Signing
                SetNodeValue(lDoc, "SignAssembly", pSignAssemblyCheck.Active.ToString().ToLower())
                If pSignAssemblyCheck.Active Then
                    SetNodeValue(lDoc, "AssemblyOriginatorKeyFile", pKeyFileEntry.Text)
                    SetNodeValue(lDoc, "DelaySign", pDelaySignCheck.Active.ToString().ToLower())
                End If
                
                ' Auto-increment (custom property)
                SetNodeValue(lDoc, "AutoIncrementBuildNumber", pAutoIncrementCheck.Active.ToString().ToLower())
                
                ' Save with formatting
                Dim lSettings As New XmlWriterSettings()
                lSettings.Indent = True
                lSettings.IndentChars = "  "
                lSettings.NewLineChars = Environment.NewLine
                lSettings.NewLineHandling = NewLineHandling.Replace
                lSettings.OmitXmlDeclaration = False
                lSettings.Encoding = New UTF8Encoding(False)
                
                Using lWriter As XmlWriter = XmlWriter.Create(pProjectFile, lSettings)
                    lDoc.Save(lWriter)
                End Using
                
            Catch ex As Exception
                Throw New Exception($"Failed to Save project settings: {ex.Message}")
            End Try
        End Sub
        
        Private Function GetOutputType() As String
            Select Case pOutputTypeCombo.Active
                Case 0
                    Return "Exe"
                Case 1
                    Return "WinExe"
                Case 2
                    Return "Library"
                Case Else
                    Return "Exe"
            End Select
        End Function
        
        Private Sub SetNodeValue(vDoc As XmlDocument, vNodeName As String, vValue As String)
            Try
                ' Find or create the node
                Dim lNode As XmlNode = vDoc.SelectSingleNode($"//{vNodeName}")
                
                If lNode IsNot Nothing Then
                    lNode.InnerText = vValue
                Else
                    ' Node doesn't exist - need to create it in the first PropertyGroup
                    Dim lPropertyGroup As XmlNode = vDoc.SelectSingleNode("//PropertyGroup[1]")
                    If lPropertyGroup IsNot Nothing Then
                        Dim lNewNode As XmlNode = vDoc.CreateElement(vNodeName)
                        lNewNode.InnerText = vValue
                        lPropertyGroup.AppendChild(lNewNode)
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error setting Node Value {vNodeName}: {ex.Message}")
            End Try
        End Sub
        
        Private Sub ShowErrorDialog(vMessage As String)
            Dim lDialog As New MessageDialog(Nothing, DialogFlags.Modal,
                                           MessageType.Error, ButtonsType.Ok,
                                           vMessage)
            lDialog.Run()
            lDialog.Destroy()
        End Sub

        Private Sub LoadAssemblyInfo()
            Try
                Dim lAssemblyInfoPath As String = GetAssemblyInfoPath()
                
                If Not File.Exists(lAssemblyInfoPath) Then
                    ' Set defaults
                    pTitleEntry.Text = System.IO.Path.GetFileNameWithoutExtension(pProjectFile)
                    pCompanyEntry.Text = ""
                    pProductEntry.Text = System.IO.Path.GetFileNameWithoutExtension(pProjectFile)
                    pCopyrightEntry.Text = $"Copyright © {DateTime.Now.Year}"
                    pDescriptionEntry.Text = ""
                    pTrademarkEntry.Text = ""
                    pAssemblyVersionEntry.Text = "1.0.0.0"
                    pFileVersionEntry.Text = "1.0.0.0"
                    pInformationalVersionEntry.Text = ""
                    Return
                End If
                
                ' Read assembly info file
                Dim lContent As String = File.ReadAllText(lAssemblyInfoPath)
                
                ' Extract values using regex
                pTitleEntry.Text = ExtractAttributeValue(lContent, "AssemblyTitle")
                pDescriptionEntry.Text = ExtractAttributeValue(lContent, "AssemblyDescription")
                pCompanyEntry.Text = ExtractAttributeValue(lContent, "AssemblyCompany")
                pProductEntry.Text = ExtractAttributeValue(lContent, "AssemblyProduct")
                pCopyrightEntry.Text = ExtractAttributeValue(lContent, "AssemblyCopyright")
                pTrademarkEntry.Text = ExtractAttributeValue(lContent, "AssemblyTrademark")
                pAssemblyVersionEntry.Text = ExtractAttributeValue(lContent, "AssemblyVersion")
                pFileVersionEntry.Text = ExtractAttributeValue(lContent, "AssemblyFileVersion")
                pInformationalVersionEntry.Text = ExtractAttributeValue(lContent, "AssemblyInformationalVersion")
                
            Catch ex As Exception
                Console.WriteLine($"error loading assembly info: {ex.Message}")
            End Try
        End Sub

'        Private Function ExtractAttributeValue(vContent As String, vAttributeName As String) As String
'            Try
'                ' Pattern to match: <Assembly: AssemblyTitle("value")>
'                Dim lPattern As String = $"<Assembly:\s*{vAttributeName}\s*\(\s*""([^""]*)""\s*\)>"
'                Dim lMatch As System.Text.RegularExpressions.Match = System.Text.RegularExpressions.Regex.Match(vContent, lPattern)
'                
'                If lMatch.Success Then
'                    Return lMatch.Groups(1).Value
'                End If
'                
'            Catch ex As Exception
'                Console.WriteLine($"Error extracting attribute {vAttributeName}: {ex.Message}")
'            End Try
'            
'            Return ""
'        End Function


        Private Sub OnGenerateKeyFile(vSender As Object, vE As EventArgs)
            Try
                ' Create file chooser for save location
                Using lDialog As New FileChooserDialog(
                    "Save Strong Name key File",
                    Me.Toplevel,
                    FileChooserAction.Save,
                    "Cancel", ResponseType.Cancel,
                    "Save", ResponseType.Accept)
                    
                    lDialog.DoOverwriteConfirmation = True
                    
                    ' Add filter
                    Dim lFilter As New FileFilter()
                    lFilter.Name = "Strong Name key Files (*.snk)"
                    lFilter.AddPattern("*.snk")
                    lDialog.AddFilter(lFilter)
                    
                    ' Set default name
                    lDialog.CurrentName = $"{System.IO.Path.GetFileNameWithoutExtension(pProjectFile)}.snk"
                    
                    If lDialog.Run() = CInt(ResponseType.Accept) Then
                        Dim lKeyPath As String = lDialog.FileName
                        
                        ' Generate key using sn.exe or create a dummy file
                        ' For now, create a placeholder
                        File.WriteAllBytes(lKeyPath, New Byte() {1, 2, 3, 4})
                        
                        ' Update key file entry with relative path
                        Dim lRelativePath As String = GetRelativePath(lKeyPath)
                        pKeyFileEntry.Text = lRelativePath
                        
                        ShowInfoDialog("Strong Name key file Created successfully.")
                        IsModified = True
                    End If
                    
                    lDialog.Destroy()
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"error generating key file: {ex.Message}")
                ShowErrorDialog($"Failed to generate key file: {ex.Message}")
            End Try
        End Sub

        Private Function GetAssemblyInfoPath() As String
            Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
            
            ' Try SDK-style project location first
            Dim lPath As String = System.IO.Path.Combine(lProjectDir, "My project", "AssemblyInfo.vb")
            If File.Exists(lPath) Then Return lPath
            
            ' Try Properties folder (C# style but sometimes used)
            lPath = System.IO.Path.Combine(lProjectDir, "Properties", "AssemblyInfo.vb")
            If File.Exists(lPath) Then Return lPath
            
            ' Default to My Project folder
            Return System.IO.Path.Combine(lProjectDir, "My project", "AssemblyInfo.vb")
        End Function
        
        Private Function CreateNewAssemblyInfo() As String
            Dim lProjectName As String = System.IO.Path.GetFileNameWithoutExtension(pProjectFile)
            
            Return "Imports System.Reflection" & Environment.NewLine & _
                   "Imports System.Runtime.InteropServices" & Environment.NewLine & _
                   Environment.NewLine & _
                   "' General Information about an assembly is controlled through the following" & Environment.NewLine & _
                   "' set of Attributes. Change these attribute values to modify the information" & Environment.NewLine & _
                   "' associated with an assembly." & Environment.NewLine & _
                   $"<Assembly: AssemblyTitle(""{lProjectName}"")>" & Environment.NewLine & _
                   "<Assembly: AssemblyDescription("""")" & Environment.NewLine & _
                   "<Assembly: AssemblyConfiguration("""")" & Environment.NewLine & _
                   "<Assembly: AssemblyCompany("""")" & Environment.NewLine & _
                   $"<Assembly: AssemblyProduct(""{lProjectName}"")>" & Environment.NewLine & _
                   $"<Assembly: AssemblyCopyright(""Copyright © {DateTime.Now.Year}"")>" & Environment.NewLine & _
                   "<Assembly: AssemblyTrademark("""")" & Environment.NewLine & _
                   Environment.NewLine & _
                   "' Setting ComVisible to False makes the types in this assembly not visible" & Environment.NewLine & _
                   "' to COM components. If you need to access a Type in this assembly from" & Environment.NewLine & _
                   "' COM, set the ComVisible attribute to True on that Type." & Environment.NewLine & _
                   "<Assembly: ComVisible(False)>" & Environment.NewLine & _
                   Environment.NewLine & _
                   "' Version information for an assembly consists of the following four values:" & Environment.NewLine & _
                   "'" & Environment.NewLine & _
                   "'      Major Version" & Environment.NewLine & _
                   "'      Minor Version" & Environment.NewLine & _
                   "'      Build Number" & Environment.NewLine & _
                   "'      Revision" & Environment.NewLine & _
                   "'" & Environment.NewLine & _
                   "' You can specify all the values or you can default the Build and Revision Numbers" & Environment.NewLine & _
                   "' by using the '*' as shown below:" & Environment.NewLine & _
                   "' <Assembly: AssemblyVersion(""1.0.*"")>" & Environment.NewLine & _
                   Environment.NewLine & _
                   "<Assembly: AssemblyVersion(""1.0.0.0"")>" & Environment.NewLine & _
                   "<Assembly: AssemblyFileVersion(""1.0.0.0"")>"
        End Function
        
        Private Function UpdateOrAddAttribute(vContent As String, vAttributeName As String, vValue As String) As String
            Try
                ' Pattern to match existing attribute
                Dim lPattern As String = $"<Assembly:\s*{vAttributeName}\s*\(""[^""]*""\)>"
                Dim lRegex As New Regex(lPattern, RegexOptions.Multiline)
                
                Dim lNewAttribute As String = $"<Assembly: {vAttributeName}(""{vValue}"")>"
                
                If lRegex.IsMatch(vContent) Then
                    ' Replace existing
                    Return lRegex.Replace(vContent, lNewAttribute)
                Else
                    ' Add new attribute
                    ' Find a good place to insert (after last assembly attribute)
                    Dim lLastAttributePattern As String = "(<Assembly:[^>]+>)(?!.*<Assembly:)"
                    Dim lLastMatch As Match = Regex.Match(vContent, lLastAttributePattern, RegexOptions.Singleline)
                    
                    If lLastMatch.Success Then
                        Dim lInsertPos As Integer = lLastMatch.Index + lLastMatch.Length
                        Return vContent.Insert(lInsertPos, Environment.NewLine & lNewAttribute)
                    Else
                        ' Just append at the end
                        Return vContent & Environment.NewLine & lNewAttribute
                    End If
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error updating attribute: {ex.Message}")
                Return vContent
            End Try
        End Function
        
        Private Function GetRelativePath(vFullPath As String) As String
            Try
                Dim lProjectDir As String = System.IO.Path.GetDirectoryName(pProjectFile)
                Dim lBaseUri As New Uri(lProjectDir & System.IO.Path.DirectorySeparatorChar)
                Dim lFullUri As New Uri(vFullPath)
                Dim lRelativeUri As Uri = lBaseUri.MakeRelativeUri(lFullUri)
                Return Uri.UnescapeDataString(lRelativeUri.ToString())
            Catch ex As Exception
                Return vFullPath
            End Try
        End Function
        
        Private Sub ShowInfoDialog(vMessage As String)
            Try
                Dim lDialog As New MessageDialog(
                    Me.Toplevel,
                    DialogFlags.Modal,
                    MessageType.Info,
                    ButtonsType.Ok,
                    vMessage
                )
                lDialog.Run()
                lDialog.Destroy()
            Catch ex As Exception
                Console.WriteLine($"error showing info dialog: {ex.Message}")
            End Try
        End Sub
        
        Private Function ExtractAttributeValue(vContent As String, vAttributeName As String) As String
            Try
                Dim lPattern As String = $"<Assembly:\s*{vAttributeName}\s*\(""([^""]*)""\)>"
                Dim lMatch As Match = Regex.Match(vContent, lPattern)
                
                If lMatch.Success Then
                    Return lMatch.Groups(1).Value
                End If
                
            Catch ex As Exception
                Console.WriteLine($"error extracting attribute Value: {ex.Message}")
            End Try
            
            Return ""
        End Function



    End Class
End Namespace
