/* Offline feasibility core; never installed. Cortex-M0, existing ROM palette.
   Reference at native speed 4: 1 s travel, 1.2 s entry, 10 s colour cycle.
   Colorful is the proposed rainbow trail; fixed RGB remains selectable. */
typedef unsigned char u8;
typedef unsigned short u16;
typedef unsigned int u32;
typedef struct {u16 age;u8 route,hue,rgb[3];} Drop;
typedef struct {Drop drops[5];u16 random,wait;u8 previous[7],last_color,fraction,phase,clock;} Rain;
typedef struct {u8 mode,brightness,speed,direction,colorful,rgb[3];} Config;
static const u8 routes[][6]={
 {8,16,24,32,255,41},
 {1,9,17,25,33,41},{1,9,17,25,33,42},
 {3,11,18,26,34,42},{3,10,18,26,34,43},
 {4,12,19,27,35,43},{4,12,19,28,36,44},
 {5,13,20,28,36,44},{5,13,21,29,37,45},
 {6,14,21,29,37,45},{6,14,22,30,38,46},
 {7,15,22,30,39,46},{7,15,22,30,38,46}};
static const u8 curve[]={0,0,1,2,4,8,12,18,26,35,46,58,70,84,98,113,128,142,157,171,185,197,209,220,229,237,243,247,251,253,254,255};
__attribute__((noinline)) static u16 rnd(Rain *s){u16 x=s->random;return s->random=(x>>1)^((0-(x&1))&0xb400);}
static u8 scale(u32 x,u8 b){x*=b;return (x+1+(x>>8))>>8;}
__attribute__((used,retain)) void rain_init(Rain *s){u8 *p=(u8*)s;for(u32 i=0;i<sizeof(*s);i++)p[i]=0;s->random=0xace1;for(u32 i=0;i<5;i++)s->drops[i].route=255;}
static void colour_clock(Rain *s){
 u8 now=*(volatile u8*)0x20000038;
 u16 fraction=s->fraction+(u8)(now-s->clock)*24;s->clock=now;
 while(fraction>=125){fraction-=125;if(++s->phase==192)s->phase=0;}
 s->fraction=fraction;
}
static const u8 *rain_colour(u32 phase){
 if(phase>=192)phase-=192;
 if(phase>=192)phase-=192;
 return (const u8*)0x4e30+phase*3;
}
__attribute__((noinline)) static void pixel(int t,u8 brightness,u8 *frame,const u8 *rgb){
 u8 falling=t>=67;if(falling)t-=67;
 u8 v=curve[(t*(falling?132:124))>>8];if(falling)v=255-v;
 v=scale(v,brightness);
 for(u32 k=0;k<3;k++)frame[k*108]=scale(v,rgb[k]);
}
__attribute__((noinline)) static void render(Rain *s,const Config *p,u8 *frame){
 u8 brightness=p->brightness;if(brightness>4)brightness=4;
 brightness=((const u8*)0x4d29)[brightness];
 for(u32 i=0;i<5;i++){Drop *d=s->drops+i;if(d->route==255)continue;
  for(u32 j=0;j<6;j++){
   u8 slot=routes[d->route][j];if(slot==255)continue;
   int t=(int)d->age-(int)(j*11);
   if((u32)t>=130)continue;
   const u8 *rgb=p->colorful?rain_colour(d->hue*16+s->phase+j*7):d->rgb;
   pixel(t,brightness,frame+slot,rgb);
  }
 }
}
void rain_step(Rain *s,const Config *p,u16 dt,u8 *frame){
 (void)dt;u16 occupied=0,used=(u16)1<<s->last_color;u8 free=255;
 colour_clock(s);
 for(u32 i=0;i<324;i++)frame[i]=0;
 for(u32 i=0;i<5;i++){Drop *d=s->drops+i;if(d->route!=255){
  if(++d->age>=185)d->route=255;
  else{occupied|=1<<d->route;used|=1<<d->hue;}}
  if(d->route==255)free=i;
 }
 if(s->wait)s->wait--;else if(free!=255){
  for(u32 attempt=0;attempt<26;attempt++){
   u8 r=rnd(s)&15;if(r>=13)continue;u16 cm=r==0?3:r<10?(7u<<(r-1)):r==10?7680:7168;if(cm&occupied)continue;
   u8 g=(r+1)>>1,end=routes[r][5];if(g&&g!=6&&end==s->previous[g])continue;
   Drop *d=s->drops+free;d->age=0;d->route=r;s->previous[g]=end;
   u8 c=rnd(s)&15;if(c>=12)c-=12;while(used&(1<<c)){if(++c==12)c=0;}d->hue=c;s->last_color=c;
   d->rgb[0]=p->rgb[0];d->rgb[1]=p->rgb[1];d->rgb[2]=p->rgb[2];
   u8 gap=rnd(s)&127;if(gap>95)gap-=32;s->wait=47+gap;break;
  }
 }
 render(s,p,frame);
}
