/* Analysis-only implementation; never link or flash without full verification.
   18 ms nominal frame at native speed 4. Entry 111 frames independent of pace.
   Row delays 18..23: mean 1.845 s traversal, approximately +/-12%.
   RGB fixed tracks the live RGB field, as in the browser preview. */
typedef unsigned char u8;
typedef unsigned short u16;
typedef unsigned int u32;
typedef struct {u16 age;u8 route,hue,rgb[3],pace;} Drop;
typedef struct {Drop drops[5];u16 random,wait;u8 previous[7],last_color,fraction,phase,clock;} Rain;
typedef struct {u8 mode,brightness,speed,direction,colorful,rgb[3];} Config;
static const u8 routes[][3]={{136,136,31},{17,17,17},{17,17,33},{51,34,34},{35,34,50},{68,51,51},{68,67,68},{85,68,68},{85,85,85},{102,85,85},{102,102,102},{119,102,103},{119,102,102},{136,153,47}};
static const u16 conflicts[]={8195,8199,8206,8220,56,112,224,448,896,1792,7680,7168,7168,8207};
static const u8 intervals[]={61,63,65,68,70,72};
static const u8 inverse[]={80,76,72,68,65,62};
static const u8 curve[]={0,0,1,2,4,8,12,18,26,35,46,58,70,84,98,113,128,142,157,171,185,197,209,220,229,237,243,247,251,253,254,255};
__attribute__((noinline)) static u16 rnd(Rain *s){u16 x=s->random;return s->random=(x>>1)^((0-(x&1))&0xb400);}
static u8 scale(u32 x,u8 b){x*=b;return (x+255)>>8;}
__attribute__((used,retain)) void rain_init(Rain *s){u8 *p=(u8*)s;for(u32 i=0;i<sizeof(*s);i++)p[i]=0;s->random=0xace1;for(u32 i=0;i<5;i++)s->drops[i].route=255;}
static void colour_clock(Rain *s){
 u8 now=*(volatile u8*)0x20000038;
 u16 fraction=s->fraction+(u8)(now-s->clock)*32;s->clock=now;
 while(fraction>=75){fraction-=75;if(++s->phase==192)s->phase=0;}
 s->fraction=fraction;
}
static const u8 *rain_colour(u32 phase){
 if(phase>=192)phase-=192;
 if(phase>=192)phase-=192;
 return (const u8*)0x4e30+phase*3;
}
__attribute__((always_inline)) static inline void pixel(int t,u16 level,u8 *frame,const u8 *rgb){
 u32 index,v;
 if(t>=111){index=((t-111)*inverse[level>>8])>>8;if(index>=32)return;v=255-curve[index];}
 else v=curve[(t*72)>>8];
 v=scale(v,(u8)level);
 for(u32 k=0;k<3;k++)frame[k*108]=scale(v,rgb[k]);
}
__attribute__((noinline)) static void render(Rain *s,const Config *p,u8 *frame){
 u8 brightness=p->brightness;if(brightness>4)brightness=4;
 brightness=((const u8*)0x4d29)[brightness];
 for(u32 i=0;i<5;i++){Drop *d=s->drops+i;if(d->route==255)continue;
  u16 level=brightness|((d->pace-18)<<8);
  u32 phase=d->hue*16+s->phase;if(phase>=192)phase-=192;
  for(u32 j=0;j<6;j++){
   u8 slot=(routes[d->route][j>>1]>>((j&1)*4))&15;if(slot==15)continue;slot+=j*8;
   int t=(int)d->age-(int)(j*d->pace);
   if((u32)t>=244)continue;
   const u8 *rgb=p->colorful?rain_colour(phase+j*7):p->rgb;
   pixel(t,level,frame+slot,rgb);
  }
 }
}
void rain_step(Rain *s,const Config *p,u16 dt,u8 *frame){
 (void)dt;u16 occupied=0;u8 free=255,base=60;
 colour_clock(s);
 for(u32 i=0;i<324;i++)frame[i]=0;
 for(u32 i=0;i<5;i++){Drop *d=s->drops+i;if(d->route!=255){
  u8 interval=(2*d->pace+26);
  if(++d->age>=interval*5)d->route=255;
  else{occupied|=1<<d->route;if(interval>base)base=interval;}}
  if(d->route==255)free=i;
 }
 if(s->wait)s->wait--;else{
  s->wait=7+(rnd(s)&7);
  if(free!=255)for(u32 attempt=0;attempt<30;attempt++){
   u8 r=rnd(s)&15;if(r>=14||conflicts[r]&occupied)continue;
   u8 g=r==13?0:(r+1)>>1,end=routes[r][2]>>4;if(g!=6&&end==s->previous[g])continue;
   Drop *d=s->drops+free;d->age=0;d->route=r;s->previous[g]=end;
   u8 c=s->last_color+5;if(c>=12)c-=12;d->hue=c;s->last_color=c;
   d->pace=18+((rnd(s)*6)>>16);u8 interval=(2*d->pace+26);if(interval>base)base=interval;
   s->wait=(base*(205+((rnd(s)&31)*106>>5)))>>8;break;
  }
 }
 render(s,(const Config*)0x200000dc,frame);
}
