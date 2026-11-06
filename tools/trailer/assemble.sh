#!/bin/bash
# Trailer assembly: frames from capture.cjs -> portrait 1080x1620 (2:3) and landscape 1920x1080, music from the game,
# the title in Lilita One over the last two seconds. Usage: tools/trailer/assemble.sh <framedir> <outdir>
set -e
F=$1; O=$2; ROOT=$(cd "$(dirname "$0")/../.." && pwd); FONT=$ROOT/assets/fonts/LilitaOne-Regular.ttf; MUSIC=$ROOT/assets/music/battle.mp3
T='HEARTHGATE'; S='Hold the gate. Rebuild the city.'
TITLE="drawbox=x=0:y=0:w=iw:h=ih:color=0x24122a@0.86:t=fill:enable='gte(t,17)',drawtext=fontfile=$FONT:text='$T':fontcolor=0xffe28c:fontsize=FS1:bordercolor=0x2a1422:borderw=BW:x=(w-text_w)/2:y=h*0.36:enable='gte(t,17)',drawtext=fontfile=$FONT:text='$S':fontcolor=0xfff4dc:fontsize=FS2:bordercolor=0x2a1422:borderw=6:x=(w-text_w)/2:y=h*0.36+FS1*1.15:enable='gte(t,17)'"
P=${TITLE//FS1/150}; P=${P//FS2/56}; P=${P//BW/12}
L=${TITLE//FS1/170}; L=${L//FS2/62}; L=${L//BW/14}
AUD="-i $MUSIC -filter_complex"
ffmpeg -y -loglevel error -framerate 30 -i $F/frame-%05d.jpg -i $MUSIC -filter_complex \
 "[0:v]split[pa][pb];[pa]scale=1080:-2,crop=1080:1620,boxblur=24:2,eq=brightness=-0.2[pbg];[pb]scale=-2:1620:flags=lanczos[pfg];[pbg][pfg]overlay=(W-w)/2:0,$P,format=yuv420p[v];[1:a]atrim=0:19,afade=t=in:d=0.3,afade=t=out:st=17.5:d=1.5,volume=0.9[a]" \
 -map "[v]" -map "[a]" -c:v libx264 -profile:v high -crf 19 -preset slow -c:a aac -b:a 128k -movflags +faststart -t 19 $O/trailer-portrait-1080x1620.mp4
ffmpeg -y -loglevel error -framerate 30 -i $F/frame-%05d.jpg -i $MUSIC -filter_complex \
 "[0:v]split[a][b];[a]scale=1920:-2,crop=1920:1080,boxblur=28:2,eq=brightness=-0.18:saturation=1.2[bg];[b]scale=-2:1080:flags=lanczos[fg];[bg][fg]overlay=(W-w)/2:0,drawtext=fontfile=$FONT:text='HEARTH':fontcolor=0xffe28c:fontsize=118:bordercolor=0x2a1422:borderw=10:x=(656-text_w)/2:y=400:enable='lt(t,17)',drawtext=fontfile=$FONT:text='GATE':fontcolor=0xffe28c:fontsize=118:bordercolor=0x2a1422:borderw=10:x=(656-text_w)/2:y=530:enable='lt(t,17)',drawtext=fontfile=$FONT:text='Idle tower defense':fontcolor=0xfff4dc:fontsize=50:bordercolor=0x2a1422:borderw=6:x=1264+(656-text_w)/2:y=420:enable='lt(t,17)',drawtext=fontfile=$FONT:text='Rebuild the city':fontcolor=0xfff4dc:fontsize=50:bordercolor=0x2a1422:borderw=6:x=1264+(656-text_w)/2:y=500:enable='lt(t,17)',drawtext=fontfile=$FONT:text='Collect new looks':fontcolor=0xfff4dc:fontsize=50:bordercolor=0x2a1422:borderw=6:x=1264+(656-text_w)/2:y=580:enable='lt(t,17)',$L,format=yuv420p[v];[1:a]atrim=0:19,afade=t=in:d=0.3,afade=t=out:st=17.5:d=1.5,volume=0.9[a]" \
 -map "[v]" -map "[a]" -c:v libx264 -profile:v high -crf 19 -preset slow -c:a aac -b:a 128k -movflags +faststart -t 19 $O/trailer-landscape-1920x1080.mp4
ls -la $O/trailer-*.mp4
