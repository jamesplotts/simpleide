' Utilities/FontMetrics.vb - Font measurement and metrics calculation (FIXED)
Imports Cairo
Imports Pango
Imports System

Namespace Utilities
    
    Public Class FontMetrics
        Implements IDisposable
        
        ' Private fields for metrics
        Private pCharWidth As Integer
        Private pCharHeight As Integer
        Private pAscent As Integer
        Private pDescent As Integer
        Private pFontDescription As Pango.FontDescription
        
        ' Public properties (single definition)
        Public ReadOnly Property CharWidth As Integer
            Get
                Return pCharWidth
            End Get
        End Property
        
        Public ReadOnly Property CharHeight As Integer
            Get
                Return pCharHeight
            End Get
        End Property
        
        Public ReadOnly Property Ascent As Integer
            Get
                Return pAscent
            End Get
        End Property
        
        Public ReadOnly Property Descent As Integer
            Get
                Return pDescent
            End Get
        End Property
        
        Public ReadOnly Property FontDescription As Pango.FontDescription
            Get
                Return pFontDescription
            End Get
        End Property
        
        ' Constructor with FontDescription and Context
        Public Sub New(vFontDescription As Pango.FontDescription, vContext As Cairo.Context)
            Try
                pFontDescription = vFontDescription.Copy()
                CalculateMetrics(vContext)
            Catch ex As Exception
                Console.WriteLine($"FontMetrics.New error: {ex.Message}")
                ' Set safe defaults
                SetDefaults()
            End Try
        End Sub
        
        ' Constructor with font string and Context
        Public Sub New(vFontString As String, vContext As Cairo.Context)
            Try
                pFontDescription = Pango.FontDescription.FromString(vFontString)
                CalculateMetrics(vContext)
            Catch ex As Exception
                Console.WriteLine($"FontMetrics.New error: {ex.Message}")
                ' Create fallback font
                pFontDescription = Pango.FontDescription.FromString("Monospace 10")
                CalculateMetrics(vContext)
            End Try
        End Sub

        ' Constructor with FontDescription only - sets defaults without calculating
        Public Sub New(vFontDescription As FontDescription)
            If vFontDescription Is Nothing Then 
                Throw New ArgumentNullException("vFontDescription", "Font Description cannot be null")
            End If
            
            Try
                pFontDescription = vFontDescription.Copy()
                ' Set reasonable defaults based on font size
                SetDefaultsFromFontDescription()
            Catch ex As Exception
                Console.WriteLine($"FontMetrics.New error: {ex.Message}")
                SetDefaults()
            End Try
        End Sub
        
        Private Sub SetDefaults()
            pCharWidth = 8
            pCharHeight = 16
            pAscent = 12
            pDescent = 4
            If pFontDescription Is Nothing Then
                pFontDescription = Pango.FontDescription.FromString("Monospace 10")
            End If
        End Sub
        
        Private Sub SetDefaultsFromFontDescription()
            Try
                ' Get font size in pixels
                Dim lSize As Integer = pFontDescription.Size
                If pFontDescription.SizeIsAbsolute Then
                    ' Size is already in device units (pixels)
                    lSize = lSize
                Else
                    ' Size is in points, convert to pixels (1 point = 1.333 pixels at 96 DPI)
                    lSize = Pango.Units.ToPixels(lSize)
                End If
                
                ' Set reasonable defaults based on font size
                ' These are approximations for monospace fonts
                pCharHeight = Math.Max(lSize, 12)
                pCharWidth = CInt(Math.Ceiling(pCharHeight * 0.6))  ' Typical monospace ratio
                pAscent = CInt(Math.Ceiling(pCharHeight * 0.75))
                pDescent = pCharHeight - pAscent
                
                Console.WriteLine($"FontMetrics defaults set: CharWidth={pCharWidth}, CharHeight={pCharHeight}, Ascent={pAscent}, Descent={pDescent}")
            Catch ex As Exception
                Console.WriteLine($"SetDefaultsFromFontDescription error: {ex.Message}")
                SetDefaults()
            End Try
        End Sub
        
        ' Fix for FontMetrics.vb CalculateMetrics method
        Private Sub CalculateMetrics(vContext As Cairo.Context)
            Try
                ' Create layout for measurement
                Using lLayout As Pango.Layout = Pango.CairoHelper.CreateLayout(vContext)
                    lLayout.FontDescription = pFontDescription
                    
                    ' Measure character width using a typical character
                    lLayout.SetText("M")  ' 'M' is typically the widest character
                    Dim lWidth As Integer = 0
                    Dim lHeight As Integer = 0
                    lLayout.GetPixelSize(lWidth, lHeight)
                    pCharWidth = lWidth
                    pCharHeight = lHeight
                    
                    ' Get font metrics for ascent/descent
                    ' FIXED: Create Pango context from Cairo context
                    Dim lPangoContext As Pango.Context = Pango.CairoHelper.CreateContext(vContext)
                    Dim lFontMap As Pango.FontMap = lPangoContext.FontMap
                    Dim lFont As Pango.Font = lPangoContext.LoadFont(pFontDescription)
                    Dim lMetrics As Pango.FontMetrics = lFont.GetMetrics(Nothing)
                    
                    ' Convert from Pango units to pixels
                    pAscent = Pango.Units.ToPixels(lMetrics.Ascent)
                    pDescent = Pango.Units.ToPixels(lMetrics.Descent)
                    
                    ' Ensure height is consistent
                    pCharHeight = Math.Max(pCharHeight, pAscent + pDescent)
                    
                    Console.WriteLine($"FontMetrics calculated: CharWidth={pCharWidth}, CharHeight={pCharHeight}, Ascent={pAscent}, Descent={pDescent}")
                End Using
                
            Catch ex As Exception
                Console.WriteLine($"FontMetrics.CalculateMetrics error: {ex.Message}")
                ' Set safe defaults
                SetDefaults()
            End Try
        End Sub
        
        ' Calculate pixel position from line/column
        Public Function GetPixelPosition(vLine As Integer, vColumn As Integer, vLineNumberWidth As Integer) As Gdk.Point
            Dim lX As Integer = vLineNumberWidth + (vColumn * pCharWidth)
            Dim lY As Integer = vLine * pCharHeight
            Return New Gdk.Point(lX, lY)
        End Function
        
        ' Calculate line/column from pixel position
        Public Function GetTextPosition(vX As Integer, vY As Integer, vLineNumberWidth As Integer, vLineHeight As Integer) As Gdk.Point
            Dim lColumn As Integer = Math.Max(0, (vX - vLineNumberWidth) \ pCharWidth)
            Dim lLine As Integer = Math.Max(0, vY \ vLineHeight)  ' Use passed Line Height
            Return New Gdk.Point(lColumn, lLine)
        End Function
        
        ' Get character bounds for cursor positioning
        Public Function GetCharacterBounds(vLine As Integer, vColumn As Integer, vLineNumberWidth As Integer) As Gdk.Rectangle
            Dim lPos As Gdk.Point = GetPixelPosition(vLine, vColumn, vLineNumberWidth)
            Return New Gdk.Rectangle(lPos.x, lPos.y, pCharWidth, pCharHeight)
        End Function
        
        ' Calculate required width for line numbers
        Public Function CalculateLineNumberWidth(vMaxLineNumber As Integer, vPadding As Integer) As Integer
            Dim lDigits As Integer = Math.Max(3, vMaxLineNumber.ToString().Length)
            Return (lDigits * pCharWidth) + (vPadding * 2)
        End Function
        
        ' Update font and recalculate metrics
        Public Sub UpdateFont(vFontString As String, vContext As Cairo.Context)
            Try
                If pFontDescription IsNot Nothing Then
                    pFontDescription.Dispose()
                End If
                
                pFontDescription = Pango.FontDescription.FromString(vFontString)
                CalculateMetrics(vContext)
                
            Catch ex As Exception
                Console.WriteLine($"UpdateFont error: {ex.Message}")
                SetDefaults()
            End Try
        End Sub
        
        ' Recalculate metrics with a new context
        Public Sub RecalculateMetrics(vContext As Cairo.Context)
            Try
                If pFontDescription IsNot Nothing Then
                    CalculateMetrics(vContext)
                Else
                    Console.WriteLine("RecalculateMetrics: No font Description set")
                    SetDefaults()
                End If
            Catch ex As Exception
                Console.WriteLine($"RecalculateMetrics error: {ex.Message}")
                SetDefaults()
            End Try
        End Sub
        
        ' Dispose resources
        Public Sub Dispose() Implements IDisposable.Dispose
            Try
                If pFontDescription IsNot Nothing Then
                    pFontDescription.Dispose()
                    pFontDescription = Nothing
                End If
            Catch ex As Exception
                Console.WriteLine($"FontMetrics.Dispose error: {ex.Message}")
            End Try
        End Sub
        
    End Class
    
End Namespace
