' MainWindow.IdentifierSync.vb - Simplified identifier case synchronization
Imports System
Imports System.Collections.Generic
Imports SimpleIDE.Editors
Imports SimpleIDE.Models
Imports SimpleIDE.Managers

Partial Public Class MainWindow
    
    ' ===== Identifier Sync Integration =====
    

    
    ''' <summary>
    ''' Handle identifier case changes from any editor
    ''' </summary>
    ''' <param name="vOldCase">The old casing of the identifier</param>
    ''' <param name="vNewCase">The new casing of the identifier</param>
    ''' <param name="vScope">The scope of the identifier</param>
    Private Sub OnEditorIdentifierCaseChanged(vOldCase As String, vNewCase As String, vScope As CustomDrawingEditor.IdentifierScope)
        Try
            ' Skip if no project manager
            If pProjectManager Is Nothing OrElse Not pProjectManager.IsProjectOpen Then Return
            
            ' Update through ProjectManager - it handles everything
            pProjectManager.UpdateIdentifierCase(vOldCase, vNewCase)
            
            ' The ProjectManager will:
            ' 1. Update its identifier map
            ' 2. Save to project metadata
            ' 3. Raise IdentifierMapUpdated event
            ' 4. Trigger re-parsing of affected files
            
            Console.WriteLine($"Identifier case changed: {vOldCase} -> {vNewCase}")
            
        Catch ex As Exception
            Console.WriteLine($"OnEditorIdentifierCaseChanged error: {ex.Message}")
        End Try
    End Sub
    
    ''' <summary>
    ''' Handle ProjectManager's IdentifierMapUpdated event
    ''' </summary>
    ''' <remarks>
    ''' Called when ProjectManager updates its identifier map.
    ''' Updates all open editors with the new map.
    ''' </remarks>
    Private Sub OnProjectManagerIdentifierMapUpdated()
        Try
            ' Get updated identifier map
            Dim lIdentifierMap As Dictionary(Of String, String) = pProjectManager.GetIdentifierCaseMap()
            
            ' Update all open editors
            for each lTabEntry in pOpenTabs
                Dim lTab As TabInfo = lTabEntry.Value
                
                ' Skip if no editor
                If lTab.Editor Is Nothing Then Continue for
                
                ' Get CustomDrawingEditor
                Dim lEditor As CustomDrawingEditor = TryCast(lTab.Editor, CustomDrawingEditor)
                If lEditor Is Nothing Then Continue for
                
                ' Clear and reload the identifier map
                lEditor.ClearIdentifierCaseMap()
                for each kvp in lIdentifierMap
                    lEditor.UpdateIdentifierCaseMap(kvp.Key, kvp.Value)
                Next
                
                ' Force repaint to show updated casing
                lEditor.QueueDraw()
            Next
            
            Console.WriteLine($"Updated {pOpenTabs.Count} editors with {lIdentifierMap.Count} identifier cases")
            
        Catch ex As Exception
            Console.WriteLine($"OnProjectManagerIdentifierMapUpdated error: {ex.Message}")
        End Try
    End Sub

    
End Class