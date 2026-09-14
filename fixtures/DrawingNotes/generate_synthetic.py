"""Regenerate the repository-owned Drawing Notes qualification PDF."""
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import landscape, letter
from reportlab.lib.colors import Color, black

output = Path(__file__).with_name("synthetic-engineering-drawing.pdf")
page = landscape(letter)
c = canvas.Canvas(str(output), pagesize=page, invariant=1, pageCompression=1)
c.setTitle("Aetheris Drawing Notes Synthetic Fixture")
c.setAuthor("Aetheris")
c.setCreator("fixtures/DrawingNotes/generate_synthetic.py")
c.setStrokeColor(black); c.setFillColor(black)

def label(text, x, y, size=7):
    c.setFont("Courier", size); c.drawString(x, y, text)

def dim(x1, y1, x2, y2, text):
    c.setLineWidth(.6); c.line(x1, y1, x2, y2)
    if y1 == y2:
        c.line(x1, y1-4, x1, y1+4); c.line(x2, y2-4, x2, y2+4)
        label(text, (x1+x2)/2-12, y1+4)
    else:
        c.line(x1-4, y1, x1+4, y1); c.line(x2-4, y2, x2+4, y2)
        label(text, x1+5, (y1+y2)/2)

label("AETHERIS DRAWING NOTES - SYNTHETIC QUALIFICATION", 28, 584, 11)
label("UNITS: mm   SCALE: NONE   SHEET 1 OF 1", 500, 584, 8)

# Front view and global datums.
label("FRONT VIEW", 135, 525, 9)
c.roundRect(90, 300, 220, 220, 16, stroke=1, fill=0)
c.line(200, 290, 200, 530); c.line(80, 410, 320, 410)
label("DATUM A - LEFT OUTER EDGE", 90, 278)
label("DATUM B - BOTTOM OUTER EDGE", 90, 264)
dim(90, 540, 310, 540, "110.00 OVERALL")
dim(70, 300, 70, 520, "160.00 OVERALL")
c.circle(250, 460, 18); label("FEATURE: PORT", 230, 435)

# Side view.
label("RIGHT SIDE VIEW", 367, 525, 9)
c.roundRect(390, 300, 30, 220, 8, stroke=1, fill=0)
dim(390, 540, 420, 540, "8.00 THICK")

# Local detail with its own datum.
label("DETAIL D - LOCAL DATUM", 485, 538, 9)
c.rect(470, 350, 155, 170, stroke=1, fill=0)
c.setDash(3,2); c.line(500, 360, 500, 510); c.line(480, 385, 615, 385); c.setDash()
label("0.00 D.X", 477, 515); label("0.00 D.Y", 628, 381)
c.circle(550, 455, 22); label("CAMERA 1", 526, 425)
dim(500, 490, 550, 490, "25.00 X")
dim(585, 385, 585, 455, "35.00 Y")

# Section and keepout are intentionally not product solids.
label("SECTION S-S", 90, 230, 9)
c.rect(100, 130, 210, 60, stroke=1, fill=0)
c.setFillColor(Color(.92,.92,.92)); c.rect(100, 130, 210, 18, stroke=1, fill=1); c.setFillColor(black)
label("PRODUCT SECTION", 112, 155)
c.setDash(4,3); c.line(145, 205, 205, 250); c.line(265, 205, 205, 250); c.setDash()
label("SENSOR KEEPOUT CONE", 145, 253)
label("FUNCTIONAL KEEPOUT - NOT PRODUCT GEOMETRY", 112, 112, 7)
dim(100, 95, 310, 95, "ALL AROUND 2.50")

label("NOTES", 470, 230, 9)
label("1. PDF REMAINS AUTHORITY.", 470, 212)
label("2. DETAIL D USES A LOCAL XY DATUM.", 470, 196)
label("3. SENSOR CONE IS AN ACCESSORY KEEPOUT.", 470, 180)
label("4. QUESTION: DOES 12.00 APPLY AT SURFACE?", 470, 164)

c.rect(28, 28, 736, 552, stroke=1, fill=0)
c.showPage(); c.save()
print(output)
