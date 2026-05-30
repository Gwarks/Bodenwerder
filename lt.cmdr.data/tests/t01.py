import Interface as I

NAME='Sokoban'

class Test:
    def __init__(Me, back):
        Me.back_cb = back
        Me.tq = I.getTextureQuad()
        # Wir nutzen den TextRenderer für Emojis als Tiles
        Me.tr = I.getTextRenderer(48.0)
        
        # Definition der Spiel-Objekte (Wand, Boden, Kiste, Ziel, Kiste auf Ziel, Spieler)
        Me.tiles = {
            '#': Me.tr.renderText('🧱'),
            ' ': Me.tr.renderText('  '),
            '$': Me.tr.renderText('📦'),
            '.': Me.tr.renderText('🎯'),
            '*': Me.tr.renderText('✅'),
            '@': Me.tr.renderText('👷'),
            '+': Me.tr.renderText('👷')
        }
        Me.reset()

    def reset(Me):
        # Klassisches Sokoban Level Layout
        lvl = [
            "  ######",
            "###    #",
            "# $    #",
            "# #$. ##",
            "# .   # ",
            "###  $# ",
            "  # . # ",
            "  #@  # ",
            "  ##### "
        ]
        max_w = max(len(r) for r in lvl)
        Me.grid = [list(r.ljust(max_w)) for r in lvl]
        # Spieler-Position finden
        for r in range(len(Me.grid)):
            for c in range(len(Me.grid[r])):
                if Me.grid[r][c] in ('@', '+'):
                    Me.py, Me.px = r, c

    def move(Me, dr, dc):
        nr, nc = Me.py + dr, Me.px + dc
        if not (0 <= nr < len(Me.grid) and 0 <= nc < len(Me.grid[0])): return
        
        cell = Me.grid[nr][nc]
        if cell == '#': return # Wand blockiert
        
        if cell in ('$', '*'): # Kiste verschieben
            nnr, nnc = nr + dr, nc + dc
            if not (0 <= nnr < len(Me.grid) and 0 <= nnc < len(Me.grid[0])): return
            b_cell = Me.grid[nnr][nnc]
            if b_cell in (' ', '.'):
                Me.grid[nnr][nnc] = '$' if b_cell == ' ' else '*'
                Me.grid[nr][nc] = ' ' if cell == '$' else '.'
                cell = Me.grid[nr][nc] # Update cell für Spieler-Bewegung
            else: return # Kiste blockiert
            
        # Spieler bewegen
        Me.grid[Me.py][Me.px] = ' ' if Me.grid[Me.py][Me.px] == '@' else '.'
        Me.py, Me.px = nr, nc
        Me.grid[Me.py][Me.px] = '@' if cell == ' ' else '+'

    def Up(Me): Me.move(-1, 0)
    def Down(Me): Me.move(1, 0)
    def Left(Me): Me.move(0, -1)
    def Right(Me): Me.move(0, 1)
    def Action(Me): Me.reset()
    def Back(Me): Me.back_cb()

    def onRender(Me, size):
        # Zentrierung des Grids
        tw, th = Me.tiles['#'].Size.X, Me.tiles['#'].Size.Y
        ox = (size.X - len(Me.grid[0]) * tw) // 2
        oy = (size.Y - len(Me.grid) * th) // 2
        
        for r in range(len(Me.grid)):
            for c in range(len(Me.grid[r])):
                t = Me.tiles.get(Me.grid[r][c])
                if t: Me.tq.render(t, I.Vector2i(ox + c * tw, oy + r * th), size)
