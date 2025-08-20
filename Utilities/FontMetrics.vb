' Utilities/FontMetrics.vb - Font measurement and metrics calculation (FIXED)
Imports Cairo
Imports Pango
Imports System

Namespace Utilities
    
    ''' <summary>
    ''' Manages font metrics calculations for consistent text rendering
    ''' </summary>
    Public Class FontMetrics
        Implements IDisposable
        
        ' Private fields for metrics
        Private pCharWidth As Integer
        Private pCharHeight As Integer
        Private pAscent As Integer
        Private pDescent As Integer
        Private pFontDescription As Pango.FontDescription
        
        ' Public properties (single definition)
        ''' <summary>
        ''' Gets the width of a single character in pixels
        ''' </summary>
        ''' <value>Character width in pixels</value>
        Public ReadOnly Property CharWidth As Integer
            Get
                Return pCharWidth
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the height of a single line in pixels
        ''' </summary>
        ''' <value>Line height in pixels</value>
        Public ReadOnly Property CharHeight As Integer
            Get
                Return pCharHeight
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the ascent of the font in pixels
        ''' </summary>
        ''' <value>Font ascent in pixels</value>
        Public ReadOnly Property Ascent As Integer
            Get
                Return pAscent
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the descent of the font in pixels
        ''' </summary>
        ''' <value>Font descent in pixels</value>
        Public ReadOnly Property Descent As Integer
            Get
                Return pDescent
            End Get
        End Property
        
        ''' <summary>
        ''' Gets the Pango font description being used
        ''' </summary>
        ''' <value>The current font description</value>
        Public ReadOnly Property FontDescription As Pango.FontDescription
            Get
                Return pFontDescription
            End Get
        End Property
        
        ''' <summary>
        ''' Creates a new FontMetrics instance with a font description and Cairo context
        ''' </summary>
        ''' <param name="vFontDescription">The font description to measure</param>
        ''' <param name="vContext">Cairo context for measurement</param>
        Public Sub New(vFontDescription As Pango.FontDescription, vContext As Cairo.Context)
            Try
                If vFontDescription Is Nothing Then
                    Throw New ArgumentNullException(NameOf(vFontDescription))
                End If
                
                pFontDescription = vFontDescription.Copy()
                CalculateMetrics(vContext)
                
            Catch ex As Exception
                Console.WriteLine($"FontMetrics.New error: {ex.Message}")
                ' Set safe defaults
                SetDefaults()
            End Try
        End Sub
        
        ''' <summary>
        ''' Creates a new FontMetrics instance with a font string and Cairo context
        ''' </summary>
        ''' <param name="vFontString">Font description string (e.g. "Monospace 11")</param>
        ''' <param name="vContext">Cairo context for measurement</param>
        Public Sub New(vFontString As String, vContext As Cairo.Context)
            Try
                pFontDescription = Pango.FontDescription.FromString(vFontString)
                CalculateMetrics(vContext)
                
            Catch ex As Exception
                Console.WriteLine($"FontMetrics.New error: {ex.Message}")
                ' Create fallback font
                pFontDescription = Pango.FontDescription.FromString("Monospace 10")
                SetDefaults()
            End Try
        End Sub

        ''' <summary>
        ''' Creates a new FontMetrics instance with only a font description (uses defaults)
        ''' </summary>
        ''' <param name="vFontDescription">The font description</param>
        Public Sub New(vFontDescription As FontDescription)
            If vFontDescription Is Nothing Then 
                Throw New ArgumentNullException(NameOf(vFontDescription), "Font Description cannot be null")
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
        
        ''' <summary>
        ''' Sets default metric values for fallback
        ''' </summary>
        Private Sub SetDefaults()
            pCharWidth = 8
            pCharHeight = 16
            pAscent = 12
            pDescent = 4
            
            If pFontDescription Is Nothing Then
                pFontDescription = Pango.FontDescription.FromString("Monospace 10")
            End If
        End Sub
        
        ''' <summary>
        ''' Sets reasonable default metrics based on font description size
        ''' </summary>
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
        
        ''' <summary>
        ''' Calculates font metrics using Cairo and Pango with robust error handling
        ''' </summary>
        ''' <param name="vContext">Cairo context for measurement</param>
        Private Sub CalculateMetrics(vContext As Cairo.Context)
            Try
                If vContext Is Nothing Then
                    Console.WriteLine("CalculateMetrics: Cairo context is Nothing, using defaults")
                    SetDefaults()
                    Return
                End If
                
                ' Create layout for measurement
                Dim lLayout As Pango.Layout = Nothing
                Try
                    lLayout = Pango.CairoHelper.CreateLayout(vContext)
                    
                    If lLayout Is Nothing Then
                        Console.WriteLine("CalculateMetrics: Could not create Pango layout, using defaults")
                        SetDefaults()
                        Return
                    End If
                    
                    lLayout.FontDescription = pFontDescription
                    
                    ' Measure character width using a typical character
                    lLayout.SetText("M")  ' 'M' is typically the widest character
                    Dim lWidth As Integer = 0
                    Dim lHeight As Integer = 0
                    lLayout.GetPixelSize(lWidth, lHeight)
                    pCharWidth = lWidth
                    pCharHeight = lHeight
                    
                    ' Try to get more precise font metrics
                    Try
                        ' FIXED: More robust handling of Pango context operations
                        Dim lPangoContext As Pango.Context = Pango.CairoHelper.CreateContext(vContext)
                        
                        If lPangoContext IsNot Nothing Then
                            Dim lFontMap As Pango.FontMap = lPangoContext.FontMap
                            
                            If lFontMap IsNot Nothing Then
                                Dim lFont As Pango.Font = lPangoContext.LoadFont(pFontDescription)
                                
                                If lFont IsNot Nothing Then
                                    Dim lMetrics As Pango.FontMetrics = lFont.GetMetrics(Nothing)
                                    
                                    If lMetrics IsNot Nothing Then
                                        ' Convert from Pango units to pixels
                                        pAscent = Pango.Units.ToPixels(lMetrics.Ascent)
                                        pDescent = Pango.Units.ToPixels(lMetrics.Descent)
                                        
                                        ' Ensure height is consistent
                                        pCharHeight = Math.Max(pCharHeight, pAscent + pDescent)
                                        
                                        Console.WriteLine($"FontMetrics calculated: CharWidth={pCharWidth}, CharHeight={pCharHeight}, Ascent={pAscent}, Descent={pDescent}")
                                    Else
                                        ' FontMetrics is null, use approximations
                                        Console.WriteLine("FontMetrics: GetMetrics returned Nothing, using approximations")
                                        pAscent = CInt(Math.Ceiling(pCharHeight * 0.75))
                                        pDescent = pCharHeight - pAscent
                                    End If
                                Else
                                    ' Font couldn't be loaded, use approximations
                                    Console.WriteLine("FontMetrics: LoadFont returned Nothing, using approximations")
                                    pAscent = CInt(Math.Ceiling(pCharHeight * 0.75))
                                    pDescent = pCharHeight - pAscent
                                End If
                            Else
                                ' FontMap is null, use approximations
                                Console.WriteLine("FontMetrics: FontMap is Nothing, using approximations")
                                pAscent = CInt(Math.Ceiling(pCharHeight * 0.75))
                                pDescent = pCharHeight - pAscent
                            End If
                        Else
                            ' Pango context creation failed, use approximations
                            Console.WriteLine("FontMetrics: CreateContext returned Nothing, using approximations")
                            pAscent = CInt(Math.Ceiling(pCharHeight * 0.75))
                            pDescent = pCharHeight - pAscent
                        End If
                        
                    Catch exPango As Exception
                        ' If precise metrics fail, use approximations based on measured height
                        Console.WriteLine($"FontMetrics: Pango operations failed ({exPango.Message}), using approximations")
                        pAscent = CInt(Math.Ceiling(pCharHeight * 0.75))
                        pDescent = pCharHeight - pAscent
                    End Try
                    
                Finally
                    ' Clean up layout
                    If lLayout IsNot Nothing Then
                        lLayout.Dispose()
                    End If
                End Try
                
            Catch ex As Exception
                Console.WriteLine($"FontMetrics.CalculateMetrics error: {ex.Message}")
                ' Set safe defaults
                SetDefaults()
            End Try
        End Sub
        
        ''' <summary>
        ''' Calculates pixel position from line and column coordinates
        ''' </summary>
        ''' <param name="vLine">Line number (0-based)</param>
        ''' <param name="vColumn">Column number (0-based)</param>
        ''' <param name="vLineNumberWidth">Width of the line number area in pixels</param>
        ''' <returns>Point containing X,Y pixel coordinates</returns>
        Public Function GetPixelPosition(vLine As Integer, vColumn As Integer, vLineNumberWidth As Integer) As Gdk.Point
            Dim lX As Integer = vLineNumberWidth + (vColumn * pCharWidth)
            Dim lY As Integer = vLine * pCharHeight
            Return New Gdk.Point(lX, lY)
        End Function
        
        ''' <summary>
        ''' Calculates line and column from pixel position
        ''' </summary>
        ''' <param name="vX">X coordinate in pixels</param>
        ''' <param name="vY">Y coordinate in pixels</param>
        ''' <param name="vLineNumberWidth">Width of the line number area in pixels</param>
        ''' <param name="vLineHeight">Height of a single line in pixels</param>
        ''' <returns>Point containing column (X) and line (Y) coordinates</returns>
        Public Function GetTextPosition(vX As Integer, vY As Integer, vLineNumberWidth As Integer, vLineHeight As Integer) As Gdk.Point
            Dim lColumn As Integer = Math.Max(0, (vX - vLineNumberWidth) \ pCharWidth)
            Dim lLine As Integer = Math.Max(0, vY \ vLineHeight)  ' Use passed Line Height
            Return New Gdk.Point(lColumn, lLine)
        End Function
        
        ''' <summary>
        ''' Gets the bounding rectangle for a character at the specified position
        ''' </summary>
        ''' <param name="vLine">Line number (0-based)</param>
        ''' <param name="vColumn">Column number (0-based)</param>
        ''' <param name="vLineNumberWidth">Width of the line number area in pixels</param>
        ''' <returns>Rectangle containing the character bounds</returns>
        Public Function GetCharacterBounds(vLine As Integer, vColumn As Integer, vLineNumberWidth As Integer) As Gdk.Rectangle
            Dim lPos As Gdk.Point = GetPixelPosition(vLine, vColumn, vLineNumberWidth)
            Return New Gdk.Rectangle(lPos.x, lPos.y, pCharWidth, pCharHeight)
        End Function
        
        ''' <summary>
        ''' Calculates the required width for line numbers display
        ''' </summary>
        ''' <param name="vMaxLineNumber">The maximum line number to display</param>
        ''' <param name="vPadding">Padding on each side in pixels</param>
        ''' <returns>Total width required for line number display</returns>
        Public Function CalculateLineNumberWidth(vMaxLineNumber As Integer, vPadding As Integer) As Integer
            Dim lDigits As Integer = Math.Max(3, vMaxLineNumber.ToString().Length)
            Return (lDigits * pCharWidth) + (vPadding * 2)
        End Function
        
        ''' <summary>
        ''' Updates the font and recalculates metrics
        ''' </summary>
        ''' <param name="vFontString">New font description string</param>
        ''' <param name="vContext">Cairo context for measurement</param>
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
        
        ''' <summary>
        ''' Recalculates metrics with a new Cairo context
        ''' </summary>
        ''' <param name="vContext">New Cairo context for measurement</param>
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
        
        ''' <summary>
        ''' Disposes of resources used by this FontMetrics instance
        ''' </summary>
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