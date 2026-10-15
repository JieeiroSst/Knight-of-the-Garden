# Mo phong terrain.gdshader (numpy) - xem truoc mat dat tung vung.
import numpy as np
from PIL import Image
T='/Users/quan/Desktop/HiepSiVeVuon/assets3d/textures/terrain/'
L=["0_grass","1_meadow","2_dirt","3_mud","4_forest","5_rock","6_sand","7_gravel"]
AH=[np.asarray(Image.open(T+l+'_albedo_height.png')).astype(np.float32)/255 for l in L]
NR=[np.asarray(Image.open(T+l+'_normal_rough.png')).astype(np.float32)/255 for l in L]
TILE=[48,70,60,60,64,140,80,36]
def srgb2lin(c): return np.power(c,2.2)
def hsh(x,y): return np.modf(np.sin(x*127.1+y*311.7)*43758.5453123)[0]%1.0
def vnoise(x,y):
    ix,iy=np.floor(x),np.floor(y); fx,fy=x-ix,y-iy
    a=hsh(ix,iy); b=hsh(ix+1,iy); c=hsh(ix,iy+1); d=hsh(ix+1,iy+1)
    ux=fx*fx*(3-2*fx); uy=fy*fy*(3-2*fy)
    return (a*(1-ux)+b*ux)*(1-uy)+(c*(1-ux)+d*ux)*uy
def fbm(x,y): return vnoise(x,y)*.55+vnoise(x*2.03+17,y*2.03+17)*.3+vnoise(x*4.11+41,y*4.11+41)*.15
def samp(tex,u,v):
    S=tex.shape[0]; i=(np.floor(u*S).astype(int))%S; j=(np.floor(v*S).astype(int))%S
    return tex[j,i]
def rot(x,y,a): c,s=np.cos(a),np.sin(a); return c*x-s*y, s*x+c*y
def render(weights, x0, z0, size=600, res=420, sun=np.array([0.4,0.8,0.45])):
    xs=np.linspace(x0,x0+size,res); X,Z=np.meshgrid(xs,np.linspace(z0,z0+size,res))
    w=[np.full_like(X,weights[k]) for k in range(8)]
    for i in range(8):
        n=0.6*fbm(X*(0.0045+i*0.0007)+i*31.7, Z*(0.0045+i*0.0007)+i*13.3)+0.4*vnoise(X*0.019+i*7.1, Z*0.019+i*3.9)
        w[i]=(w[i]*(0.1+1.8*n))**1.6
    W=np.stack(w,0); order=np.argsort(-W,axis=0); i1=order[0]; i2=order[1]
    w1=np.take_along_axis(W,i1[None],0)[0]; w2=np.take_along_axis(W,i2[None],0)[0]; b2=w2/np.maximum(w1+w2,1e-4)
    mixt=np.clip((vnoise(X*0.012+7,Z*0.012+7)-0.3)/0.4,0,1); mixt=mixt*mixt*(3-2*mixt)*0.55
    def layer(idx):
        out=np.zeros(X.shape+(4,)); nout=np.zeros(X.shape+(4,))
        for k in range(8):
            m=idx==k
            if not m.any(): continue
            t=TILE[k]; u1,v1=X[m]/t,Z[m]/t; ang=1.1+k*0.7; rx,rz=rot(X[m],Z[m],ang)
            u2,v2=rx/(t*2.37)+0.37, rz/(t*2.37)+0.71
            a=samp(AH[k],u1,v1)*(1-mixt[m][:,None])+samp(AH[k],u2,v2)*mixt[m][:,None]
            n=samp(NR[k],u1,v1)*(1-mixt[m][:,None])+samp(NR[k],u2,v2)*mixt[m][:,None]
            out[m]=a; nout[m]=n
        return out,nout
    a1,n1=layer(i1); a2,n2=layer(i2)
    ha=a1[...,3]+(1-b2); hb=a2[...,3]+b2; mm=np.maximum(ha,hb)-0.18
    k1=np.maximum(ha-mm,0); k2=np.maximum(hb-mm,0); t=(k2/np.maximum(k1+k2,1e-4))[...,None]
    alb=a1[...,:3]*(1-t)+a2[...,:3]*t; nrm=n1[...,:3]*(1-t)+n2[...,:3]*t
    g7=((i1==7)*(1-t[...,0])+(i2==7)*t[...,0]); alb=alb*(1-g7[...,None])+alb*np.array([0.62,0.59,0.53])*g7[...,None]
    macro=fbm(X*0.0016+3,Z*0.0016+3); macro2=vnoise(X*0.0007+11,Z*0.0007+11)
    alb*=(0.86+0.24*macro)[...,None]
    dry=(alb@np.array([.45,.4,.15]))[...,None]*np.array([1.08,1,.72]); dm=np.clip((macro2-0.55)/0.35,0,1); dm=dm*dm*(3-2*dm)*0.35
    alb=alb*(1-dm[...,None])+dry*dm[...,None]
    N=nrm*2-1; N=np.stack([N[...,0],N[...,2],-N[...,1]],-1); N/=np.linalg.norm(N,axis=-1,keepdims=True)
    lit=np.clip(N@ (sun/np.linalg.norm(sun)),0,1)*0.85+0.3
    col=np.clip(alb*lit[...,None],0,1)
    return Image.fromarray((col*255).astype(np.uint8))
