from PIL import Image

def lerp_color(c1, c2, t):
    """Linearly interpolate between two RGB colors"""
    return tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3))

def quantize_t(t, steps=3):
    """Quantize t to discrete steps for banded gradient"""
    return round(t * steps) / steps

def is_socket_corner(x, y, size):
    """Socket has 3 pixels cut from each corner (2x2 minus inner corner)"""
    # Top-left: (0,0), (1,0), (0,1)
    if (x, y) in [(0, 0), (1, 0), (0, 1)]:
        return True
    # Top-right: (size-1,0), (size-2,0), (size-1,1)
    if (x, y) in [(size-1, 0), (size-2, 0), (size-1, 1)]:
        return True
    # Bottom-left: (0,size-1), (1,size-1), (0,size-2)
    if (x, y) in [(0, size-1), (1, size-1), (0, size-2)]:
        return True
    # Bottom-right: (size-1,size-1), (size-2,size-1), (size-1,size-2)
    if (x, y) in [(size-1, size-1), (size-2, size-1), (size-1, size-2)]:
        return True
    return False

def is_fill_corner(x, y, size):
    """Fill has 1 pixel cut from each corner"""
    return (x in (0, size-1)) and (y in (0, size-1))

def create_socket(size=10, border_top=(25, 25, 30), border_bot=(15, 15, 20)):
    """Dark socket with rounded corners, semi-transparent, diagonal banded gradient"""
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    
    # Fill is always neutral dark
    fill_top = (20, 20, 25)
    fill_bot = (10, 10, 15)
    
    # Diagonal uses x+y for gradient
    max_diag = (size - 1) * 2
    
    for y in range(size):
        for x in range(size):
            if is_socket_corner(x, y, size):
                continue
            
            # Diagonal gradient: top-left to bottom-right
            t = (x + y) / max_diag if max_diag > 0 else 0
            t = quantize_t(t, steps=3)
            border_color = lerp_color(border_top, border_bot, t)
            fill_color = lerp_color(fill_top, fill_bot, t)
            
            is_border = x == 0 or x == size-1 or y == 0 or y == size-1
            # Also border the pixels next to cut corners
            is_border = is_border or (x == 1 and y == 0) or (x == 0 and y == 1)
            is_border = is_border or (x == size-2 and y == 0) or (x == size-1 and y == 1)
            is_border = is_border or (x == 1 and y == size-1) or (x == 0 and y == size-2)
            is_border = is_border or (x == size-2 and y == size-1) or (x == size-1 and y == size-2)
            
            if is_border:
                img.putpixel((x, y), (*border_color, 180))
            else:
                img.putpixel((x, y), (*fill_color, 140))
    
    return img

def create_fill(size=8, top_color=(150, 150, 150), bot_color=(120, 120, 120), highlight_color=(220, 220, 220)):
    """Fill gem - no border, diagonal banded gradient with sparkle highlight"""
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    
    max_diag = (size - 1) * 2
    
    for y in range(size):
        for x in range(size):
            if is_fill_corner(x, y, size):
                continue
            
            # Diagonal gradient: top-left to bottom-right
            t = (x + y) / max_diag if max_diag > 0 else 0
            t = quantize_t(t, steps=2)
            row_color = lerp_color(top_color, bot_color, t)
            
            img.putpixel((x, y), (*row_color, 255))
    
    # Add sparkle highlight (top-left inner area)
    if size >= 6:
        img.putpixel((2, 2), (*highlight_color, 255))
    
    return img

# Create the sprites
# Gray socket - pinky-purple tinted border
socket_gray = create_socket(10,
    border_top=(50, 35, 55),
    border_bot=(35, 22, 40))
socket_gray.save('Assets/UI/SanitySocketGray.png')

# Gold socket - warm tinted border
socket_gold = create_socket(10,
    border_top=(50, 40, 25),
    border_bot=(30, 25, 15))
socket_gold.save('Assets/UI/SanitySocketGold.png')

# Pinky-purple fill - brighter brain-like color
gray_fill = create_fill(
    size=8,
    top_color=(180, 120, 170),
    bot_color=(140, 85, 130),
    highlight_color=(220, 180, 210)
)
gray_fill.save('Assets/UI/SanityGray.png')

gold_fill = create_fill(
    size=8,
    top_color=(255, 210, 80),
    bot_color=(220, 170, 40),
    highlight_color=(255, 245, 200)
)
gold_fill.save('Assets/UI/SanityGold.png')

# Dark orange-red socket - danger tinted border (suffocating)
socket_red = create_socket(10,
    border_top=(60, 25, 15),
    border_bot=(40, 15, 10))
socket_red.save('Assets/UI/SanitySocketRed.png')

# Dark orange-red fill - danger color (suffocating)
red_fill = create_fill(
    size=8,
    top_color=(200, 80, 40),
    bot_color=(160, 50, 20),
    highlight_color=(240, 140, 80)
)
red_fill.save('Assets/UI/SanityRed.png')

print("Created sprites in Assets/UI/")
