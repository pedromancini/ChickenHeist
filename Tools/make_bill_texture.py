# The electricity bill on Elias' desk in the opening cinematic (Assets/Resources/Cinematics/ContaDeEnergia.png): a
# printed utility bill with the company header, the customer, the reading, a consumption chart, the amount due, a
# disconnection warning stamp and a payment slip with its barcode. Fictional company and data.
# Run:  python Tools/make_bill_texture.py
import os, random
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, '..', 'Assets', 'Resources', 'Cinematics', 'ContaDeEnergia.png')
W, H = 1024, 1254                      # the paper on the desk is 31 x 38 cm
FONTS = 'C:/Windows/Fonts/'
def font(name, size): return ImageFont.truetype(FONTS + name, size)
REG, BOLD, MONO = 'arial.ttf', 'arialbd.ttf', 'consola.ttf'
INK, GREY, LIGHT, GREEN, RED = (38, 34, 30), (110, 104, 96), (214, 208, 196), (34, 102, 64), (178, 34, 30)

paper = Image.new('RGB', (W, H), (243, 238, 224))
d = ImageDraw.Draw(paper)
rng = random.Random(7)
# a little grain and an old fold across the middle
for _ in range(9000):
    x, y = rng.randrange(W), rng.randrange(H); c = rng.randrange(225, 240)
    d.point((x, y), fill=(c, c - 4, c - 14))
d.line((0, H // 2, W, H // 2 + 6), fill=(226, 220, 204), width=3)

M = 56
# header: the company
d.rectangle((M, M, W - M, M + 120), fill=GREEN)
d.ellipse((M + 18, M + 18, M + 102, M + 102), outline=(240, 236, 220), width=6)
d.polygon([(M + 64, M + 30), (M + 46, M + 66), (M + 62, M + 66), (M + 52, M + 92), (M + 80, M + 52), (M + 64, M + 52), (M + 74, M + 30)], fill=(240, 220, 90))
d.text((M + 124, M + 22), 'COOPERATIVA VALE VERDE', font=font(BOLD, 40), fill=(245, 242, 230))
d.text((M + 124, M + 72), 'Energia Rural  ·  CNPJ 00.000.000/0001-00', font=font(REG, 24), fill=(214, 232, 216))
d.text((W - M - 262, M + 132), 'CONTA DE ENERGIA', font=font(BOLD, 28), fill=INK)

y = M + 178
d.text((M, y), 'CLIENTE', font=font(BOLD, 20), fill=GREY)
d.text((M, y + 24), 'ELIAS  —  SÍTIO BOA ESPERANÇA', font=font(BOLD, 30), fill=INK)
d.text((M, y + 62), 'Estrada Municipal do Bairro Alto, km 7  ·  Zona Rural', font=font(REG, 22), fill=INK)
d.text((M, y + 90), 'Unidade consumidora 4021-77  ·  Classe: rural', font=font(REG, 22), fill=INK)

# reading boxes
y += 140
boxes = [('REFERÊNCIA', 'SET/2026'), ('LEITURA ANTERIOR', '18 482'), ('LEITURA ATUAL', '18 731'), ('CONSUMO', '249 kWh')]
bw = (W - 2 * M) / 4
for i, (k, v) in enumerate(boxes):
    x0 = M + i * bw
    d.rectangle((x0, y, x0 + bw - 8, y + 86), outline=INK, width=2)
    d.text((x0 + 12, y + 10), k, font=font(BOLD, 17), fill=GREY)
    d.text((x0 + 12, y + 38), v, font=font(BOLD, 30), fill=INK)

# consumption history
y += 118
d.text((M, y), 'HISTÓRICO DE CONSUMO (kWh)', font=font(BOLD, 20), fill=GREY)
months = ['OUT', 'NOV', 'DEZ', 'JAN', 'FEV', 'MAR', 'ABR', 'MAI', 'JUN', 'JUL', 'AGO', 'SET']
values = [168, 175, 190, 204, 199, 181, 172, 176, 188, 214, 231, 249]
cx0, cy1, ch = M + 10, y + 190, 150
step = (W - 2 * M - 20) / len(months)
d.line((M, cy1, W - M, cy1), fill=INK, width=2)
for i, (mname, v) in enumerate(zip(months, values)):
    h = v / 260 * ch; x = cx0 + i * step
    d.rectangle((x + 8, cy1 - h, x + step - 10, cy1), fill=RED if i == len(months) - 1 else (126, 142, 128))
    d.text((x + 10, cy1 + 6), mname, font=font(REG, 17), fill=GREY)

# charges table
y = cy1 + 48
d.text((M, y), 'DESCRIÇÃO', font=font(BOLD, 20), fill=GREY); d.text((W - M - 150, y), 'VALOR (R$)', font=font(BOLD, 20), fill=GREY)
rows = [('Consumo de energia  249 kWh', '187,42'), ('Bandeira tarifária vermelha', '21,90'), ('Contribuição de iluminação pública', '9,37'),
        ('Parcelas em atraso (2 meses)', '398,15'), ('Multa e juros por atraso', '24,66')]
for i, (k, v) in enumerate(rows):
    yy = y + 34 + i * 36
    if i % 2 == 0: d.rectangle((M, yy - 4, W - M, yy + 30), fill=(234, 228, 212))
    d.text((M + 8, yy), k, font=font(REG, 24), fill=INK)
    d.text((W - M - 130, yy), v, font=font(REG, 24), fill=INK)
y += 34 + len(rows) * 36 + 12
d.line((M, y, W - M, y), fill=INK, width=2)
d.text((M + 8, y + 14), 'TOTAL A PAGAR', font=font(BOLD, 34), fill=INK)
d.text((W - M - 200, y + 10), '641,50', font=font(BOLD, 44), fill=INK)
d.text((M + 8, y + 62), 'VENCIMENTO  05/10/2026', font=font(BOLD, 26), fill=RED)

# disconnection stamp, slightly turned, over the consumption chart
stamp = Image.new('RGBA', (600, 150), (0, 0, 0, 0)); sd = ImageDraw.Draw(stamp)
sd.rectangle((6, 6, 594, 144), outline=RED + (220,), width=8)
sd.text((50, 20), 'AVISO DE CORTE', font=font(BOLD, 56), fill=RED + (220,))
sd.text((44, 94), 'Fornecimento suspenso após 15/10/2026', font=font(BOLD, 24), fill=RED + (220,))
stamp = stamp.rotate(9, expand=True, resample=Image.BICUBIC)
paper.paste(stamp, ((W - stamp.width) // 2 + 60, cy1 - 175), stamp)

# payment slip: perforation, line of digits and barcode
y += 120
for x in range(M, W - M, 18): d.line((x, y, x + 9, y), fill=GREY, width=2)
d.text((M, y + 18), 'Autenticação mecânica  ·  Pagável em lotéricas e correspondentes', font=font(REG, 18), fill=GREY)
d.text((M, y + 46), '83660000006 4  41500093402 1  10777402102 6  60500920261 0', font=font(MONO, 27), fill=INK)
bx, by = M, y + 92
while bx < W - M - 4:
    w = rng.choice((2, 2, 3, 4, 5))
    if rng.random() < .58: d.rectangle((bx, by, bx + w - 1, by + 70), fill=INK)
    bx += w
paper.save(OUT)
print('bill written', OUT, paper.size)
