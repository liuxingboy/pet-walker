using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Web.Script.Serialization;

// ImageGen supplies the character layers; this renderer moves their joints without
// regenerating faces, clothes or accessories between animation frames.
public sealed class RightRigLayout {
    public float nearHipX,nearHipY,farHipX,farHipY;
    public float nearKneeX,nearKneeY,farKneeX,farKneeY;
    public float nearOffsetX,farOffsetX;
}

static class RightWalkRig {
    public const int FrameCount=32;
    public static void ExportPreview(string root){
        string folder=Path.Combine(root,"qa","right-walk-rig","runtime");Directory.CreateDirectory(folder);
        var frames=Load(Path.Combine(root,"assets","rig"));
        var legFrames=LoadLegs(Path.Combine(root,"assets","rig"));
        using(var sheet=new Bitmap(192*8,224*4))using(var g=Graphics.FromImage(sheet))using(var font=new Font("Arial",9)){
            g.Clear(Color.FromArgb(232,240,248));
            for(int i=0;i<frames.Length;i++){
                frames[i].Save(Path.Combine(folder,"frame-"+i.ToString("D2")+".png"),ImageFormat.Png);
                legFrames[i].Save(Path.Combine(folder,"legs-"+i.ToString("D2")+".png"),ImageFormat.Png);
                int x=i%8*192,y=i/8*224;g.DrawString(i.ToString(),font,Brushes.SteelBlue,x+4,y);g.DrawImageUnscaled(frames[i],x,y+16);
                frames[i].Dispose();
                legFrames[i].Dispose();
            }
            sheet.Save(Path.Combine(folder,"contact.png"),ImageFormat.Png);
        }
    }
    public static Bitmap[] Load(string directory) {
        return LoadInternal(directory,true);
    }
    public static Bitmap[] LoadLegs(string directory) {
        return LoadInternal(directory,false);
    }
    static Bitmap[] LoadInternal(string directory,bool includeBody) {
        var layout=new JavaScriptSerializer().Deserialize<RightRigLayout>(File.ReadAllText(Path.Combine(directory,"rig.json")));
        if(layout==null)throw new InvalidDataException("Missing right-walk rig layout");
        ValidateJoint(layout.nearHipX,layout.nearHipY,layout.nearKneeX,layout.nearKneeY);
        ValidateJoint(layout.farHipX,layout.farHipY,layout.farKneeX,layout.farKneeY);
        using(var body=Read(directory,"body.png"))using(var near=Read(directory,"near-leg.png"))using(var far=Read(directory,"far-leg.png")){
            var frames=new Bitmap[FrameCount];
            for(int i=0;i<frames.Length;i++){
                double phase=i/(double)FrameCount;
                var frame=new Bitmap(192,208,PixelFormat.Format32bppArgb);
                using(var g=Graphics.FromImage(frame)){
                    g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.SmoothingMode=SmoothingMode.AntiAlias;
                    DrawLeg(g,far,layout.farHipX,layout.farHipY,layout.farKneeX,layout.farKneeY,(phase+.5)%1,layout.farOffsetX);
                    DrawLeg(g,near,layout.nearHipX,layout.nearHipY,layout.nearKneeX,layout.nearKneeY,phase,layout.nearOffsetX);
                    if(includeBody)g.DrawImage(body,0,(float)(.45*Math.Cos(phase*Math.PI*4)),192,208);
                }
                frames[i]=frame;
            }
            return frames;
        }
    }
    static Bitmap Read(string dir,string name){var b=new Bitmap(Path.Combine(dir,name));if(b.Width!=192||b.Height!=208){b.Dispose();throw new InvalidDataException("Right-walk layer size: "+name);}return b;}
    static void ValidateJoint(float hx,float hy,float kx,float ky){if(!(hx>0&&hx<192&&hy>80&&hy<195&&kx>0&&kx<192&&ky>hy&&ky<200))throw new InvalidDataException("Invalid right-walk joints");}
    static void DrawLeg(Graphics g,Bitmap image,float hx,float hy,float kx,float ky,double phase,float offsetX){
        // During the planted half-cycle the foot travels backwards relative to
        // the body. The other half swings forward with a softly bending knee.
        double footOffset=phase<.5?10.08-40.32*phase:-10.08*Math.Cos((phase-.5)*2*Math.PI);
        double legLength=Math.Max(30,198-hy);
        float thigh=(float)(-Math.Asin(footOffset/legLength)*180/Math.PI);
        float knee=phase<.5?0:(float)(24*Math.Sin((phase-.5)*2*Math.PI));
        float lift=phase<.5?0:(float)(1.4*Math.Sin((phase-.5)*2*Math.PI));
        var saved=g.Save();
        g.TranslateTransform(offsetX,0);
        g.TranslateTransform(hx,hy-lift);g.RotateTransform(thigh);g.TranslateTransform(-hx,-hy);
        int split=(int)Math.Round(ky);
        // The one-pixel overlap closes the seam at the opaque white stocking.
        g.DrawImage(image,new Rectangle(0,0,192,split+1),new Rectangle(0,0,192,split+1),GraphicsUnit.Pixel);
        g.TranslateTransform(kx,ky);g.RotateTransform(knee);g.TranslateTransform(-kx,-ky);
        g.DrawImage(image,new Rectangle(0,split,192,208-split),new Rectangle(0,split,192,208-split),GraphicsUnit.Pixel);
        g.Restore(saved);
    }
}
