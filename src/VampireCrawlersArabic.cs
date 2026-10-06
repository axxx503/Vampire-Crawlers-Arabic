using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using RTLTMPro;
using Nosebleed.Pancake.View;
using UnityEngine.Localization.Tables;
namespace VampireCrawlersArabic {
[BepInPlugin("local.vampirecrawlers.arabic","Vampire Crawlers Arabic","0.1.9")]
public class Plugin:BasePlugin {
 static Plugin Self; static TMP_FontAsset Font; static bool Failed;
 internal class Translation {public string en {get;set;} public string ar {get;set;}}
 static Dictionary<string,Translation> Translations;
 static Dictionary<int,State> States=new(); static Dictionary<string,string> Cache=new();static Dictionary<int,TMP_FontAsset> FontsByOriginal=new();
 static Dictionary<int,int> CardTraceCounts=new();static int CardTraceTotal;
 static Dictionary<string,string> SymbolAliases=new(StringComparer.OrdinalIgnoreCase);
 static readonly Regex NumericFontRun=new Regex(@"<font=""([^""]+)""([^>]*)>([-+0-9][0-9.,%+\- ]*)</font>",RegexOptions.IgnoreCase);
 class State {public TMP_FontAsset Font,ActiveFont;public Material Mat;public bool Rtl,Auto,NeedsRefresh,Restoring;public float Size,Min,Max;public string Input,Output;}
 public override void Load(){
  Self=this;Translations=JsonSerializer.Deserialize<Dictionary<string,Translation>>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location),"strings-ar.json")));var h=new Harmony("local.vampirecrawlers.arabic");
  foreach(var method in typeof(StringTableEntry).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly))if(method.Name=="GetLocalizedString" || method.Name=="GetOrCreateFormatCache")h.Patch(method,prefix:new HarmonyMethod(typeof(Plugin),nameof(TranslateEntry)));
  h.Patch(AccessTools.PropertyGetter(typeof(StringTableEntry),"Value"),prefix:new HarmonyMethod(typeof(Plugin),nameof(TranslateEntry)));
  h.Patch(AccessTools.PropertySetter(typeof(TMP_Text),"text"),prefix:new HarmonyMethod(typeof(Plugin),nameof(TextPrefix)),postfix:new HarmonyMethod(typeof(Plugin),nameof(TextPostfix)));
  h.Patch(AccessTools.PropertyGetter(typeof(TMP_Text),"text"),postfix:new HarmonyMethod(typeof(Plugin),nameof(TextGetter)));
  h.Patch(AccessTools.PropertySetter(typeof(TMP_Text),"font"),prefix:new HarmonyMethod(typeof(Plugin),nameof(FontPrefix)));
  foreach(var property in new[]{"fontSharedMaterial","fontMaterial"})h.Patch(AccessTools.PropertySetter(typeof(TMP_Text),property),prefix:new HarmonyMethod(typeof(Plugin),nameof(MaterialPrefix)));
  h.Patch(AccessTools.Method(typeof(TextMeshProUGUI),"OnPreRenderCanvas"),prefix:new HarmonyMethod(typeof(Plugin),nameof(RenderFontPrefix)));
  foreach(var ts in new[]{new[]{typeof(string)},new[]{typeof(string),typeof(bool)}}){var m=AccessTools.Method(typeof(TMP_Text),"SetText",ts);if(m!=null)h.Patch(m,prefix:new HarmonyMethod(typeof(Plugin),nameof(TextPrefix)),postfix:new HarmonyMethod(typeof(Plugin),nameof(TextPostfix)));}
  foreach(var m in typeof(TMP_Text).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly)){if(m.Name!="SetText")continue;var args=m.GetParameters();if(args.Length<2||args[0].ParameterType!=typeof(string))continue;bool numeric=true;for(int i=1;i<args.Length;i++)if(args[i].ParameterType!=typeof(float))numeric=false;if(numeric)h.Patch(m,postfix:new HarmonyMethod(typeof(Plugin),nameof(NumericTextPostfix)));}
  foreach(var t in new[]{typeof(RTLTextMeshPro),typeof(RTLTextMeshPro3D)}){
   h.Patch(AccessTools.Method(t,"UpdateText"),prefix:new HarmonyMethod(typeof(Plugin),nameof(NativeTextPrefix)),postfix:new HarmonyMethod(typeof(Plugin),nameof(NativeTextPostfix)));
   h.Patch(AccessTools.Method(t,"GetFixedText"),prefix:new HarmonyMethod(typeof(Plugin),nameof(InlineTextPrefix)));
  }
  h.Patch(AccessTools.Method(typeof(CardView),"Update"),postfix:new HarmonyMethod(typeof(Plugin),nameof(CardUpdatePostfix)));
  Log.LogInfo("Arabic runtime loaded; corrected Arabic Pixel 2 font.");
 }
 static void TranslateEntry(StringTableEntry __instance){try {var d=__instance.Data;if(d==null)return;if(Translations.TryGetValue(d.Id.ToString(),out var tr)&&d.Localized==tr.en&&tr.ar!=tr.en){d.Localized=tr.ar;__instance.FormatCache=null;}}catch(Exception e){if(!Failed){Failed=true;Self.Log.LogError(e);}}}
 static void InlineTextPrefix(TMP_Text __instance,ref string __0){if(!HasArabic(__0))return;GetFontFor(__instance.font);__0=NormalizeInlineNumbers(__0);}
 internal static string NormalizeInlineNumbers(string input){if(!HasArabic(input))return input;return NumericFontRun.Replace(input,new MatchEvaluator(ReplaceNumericFont));}
 static string ReplaceNumericFont(Match m){return SymbolAliases.TryGetValue(m.Groups[1].Value,out string alias)?"<font=\""+alias+"\""+m.Groups[2].Value+">"+m.Groups[3].Value+"</font>":m.Value;}
 static void NativeTextPrefix(TMP_Text __instance){if(__instance is RTLTextMeshPro ui){ui.PreserveNumbers=true;ui.Farsi=false;}else if(__instance is RTLTextMeshPro3D mesh){mesh.PreserveNumbers=true;mesh.Farsi=false;}}
 static void NativeTextPostfix(TMP_Text __instance){try{if(__instance==null)return;string src=__instance is RTLTextMeshPro r?r.OriginalText:((RTLTextMeshPro3D)__instance).OriginalText;int id=__instance.GetInstanceID();if(HasArabic(src)){if(!States.TryGetValue(id,out State st)){st=Remember(__instance);States[id]=st;}var f=GetFontFor(st.Font);bool changed=st.Input!=src||__instance.font!=f;st.ActiveFont=f;if(__instance.font!=f){__instance.font=f;__instance.fontSharedMaterial=st.Mat==null?f.material:TMP_MaterialManager.GetFallbackMaterial(st.Mat,f.material);__instance.UpdateMeshPadding();}FitText(__instance,st);st.Input=src;if(changed)__instance.ForceMeshUpdate(true,true);}else if(States.TryGetValue(id,out State st)){Restore(__instance,st,false);States.Remove(id);}}catch(Exception e){if(!Failed){Failed=true;Self.Log.LogError(e);}}}
 static bool IsArabicFont(TMP_FontAsset f){if(f==null)return false;if(f==Font)return true;foreach(var pair in FontsByOriginal)if(pair.Value==f)return true;return false;}
 static bool UsesArabic(TMP_Text t){if(t==null)return false;if(States.TryGetValue(t.GetInstanceID(),out State st)&&st.Restoring)return false;return HasArabic(t is RTLTextMeshPro r?r.OriginalText:t is RTLTextMeshPro3D m?m.OriginalText:t.text);}
 static void FontPrefix(TMP_Text __instance,ref TMP_FontAsset __0){try{if(__0==null||IsArabicFont(__0)||!UsesArabic(__instance))return;var original=__0;if(!States.TryGetValue(__instance.GetInstanceID(),out State st)){st=Remember(__instance);States[__instance.GetInstanceID()]=st;}__0=GetFontFor(original);st.Font=original;st.Mat=original.material;st.ActiveFont=__0;}catch(Exception e){Self.Log.LogError(e);}}
 static void MaterialPrefix(TMP_Text __instance,ref Material __0){try{if(__0==null||!UsesArabic(__instance)||!IsArabicFont(__instance.font))return;var target=__instance.font.material;if(__0.mainTexture!=target.mainTexture){if(States.TryGetValue(__instance.GetInstanceID(),out State st))st.Mat=__0;__0=TMP_MaterialManager.GetFallbackMaterial(__0,target);}}catch(Exception e){Self.Log.LogError(e);}}
 static void RenderFontPrefix(TextMeshProUGUI __instance){try{if(!UsesArabic(__instance))return;var preset=__instance.fontSharedMaterial;bool changed=false;if(!IsArabicFont(__instance.font)){if(!States.TryGetValue(__instance.GetInstanceID(),out State st)){st=Remember(__instance);States[__instance.GetInstanceID()]=st;}var target=GetFontFor(__instance.font);st.ActiveFont=target;__instance.font=target;changed=true;}var material=__instance.font.material;if(changed||preset==null||preset.mainTexture!=material.mainTexture){__instance.fontSharedMaterial=preset==null?material:TMP_MaterialManager.GetFallbackMaterial(preset,material);__instance.UpdateMeshPadding();}}catch(Exception e){Self.Log.LogError(e);}}
 internal static TMP_FontAsset GetFontFor(TMP_FontAsset original){var baseFont=GetFont();if(original==null)return baseFont;if(IsArabicFont(original))return original;int id=original.GetInstanceID();if(FontsByOriginal.TryGetValue(id,out var cached)&&cached!=null)return cached;
  // Inline <font> tags can select the original font without invoking TMP_Text.font.
  // The base Arabic atlas has no fallback, keeping this graph free of cycles.
  var originals=original.fallbackFontAssetTable;if(originals==null){originals=new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();original.fallbackFontAssetTable=originals;}if(!originals.Contains(baseFont))originals.Add(baseFont);
  var copy=UnityEngine.Object.Instantiate(baseFont);copy.name=baseFont.name+" / "+original.name;var fallback=new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();var symbols=CreateAlignedSymbols(original,baseFont);if(symbols!=null)fallback.Add(symbols);fallback.Add(original);copy.fallbackFontAssetTable=fallback;UnityEngine.Object.DontDestroyOnLoad(copy);FontsByOriginal[id]=copy;return copy;}
 static TMP_FontAsset CreateAlignedSymbols(TMP_FontAsset original,TMP_FontAsset arabic){
  if(!original.characterLookupTable.TryGetValue(48,out var zero)||!arabic.characterLookupTable.TryGetValue(0xfe8d,out var alef))return null;
  float arUnit=arabic.faceInfo.scale/arabic.faceInfo.pointSize*alef.scale*alef.glyph.scale;
  float srcUnit=original.faceInfo.scale/original.faceInfo.pointSize*zero.scale*zero.glyph.scale;
  float ratio=alef.glyph.metrics.height*arUnit/(zero.glyph.metrics.height*srcUnit);
  if(ratio<.5f||ratio>2.5f)return null;
  float shift=((alef.glyph.metrics.horizontalBearingY-alef.glyph.metrics.height)*arUnit)/(srcUnit*ratio)-(zero.glyph.metrics.horizontalBearingY-zero.glyph.metrics.height);
  var result=UnityEngine.Object.Instantiate(original);result.name="Arabic-aligned symbols / "+original.name;result.atlasPopulationMode=AtlasPopulationMode.Static;
  var face=result.faceInfo;face.ascentLine+=shift;face.descentLine+=shift;face.capLine+=shift;face.meanLine+=shift;result.faceInfo=face;
  var chars=new Il2CppSystem.Collections.Generic.List<TMP_Character>();var glyphs=new Il2CppSystem.Collections.Generic.List<UnityEngine.TextCore.Glyph>();var seen=new Dictionary<uint,UnityEngine.TextCore.Glyph>();
  foreach(char c in "0123456789!?.:,;%+-=()[]/\\\u2026\u2013\u2014")if(original.characterLookupTable.TryGetValue(c,out var source)){
   var g=source.glyph;if(!seen.TryGetValue(g.index,out var cloned)){var m=g.metrics;m.horizontalBearingY+=shift;cloned=new UnityEngine.TextCore.Glyph(g.index,m,g.glyphRect,g.scale,g.atlasIndex);seen[g.index]=cloned;glyphs.Add(cloned);}var ch=new TMP_Character(c,result,cloned);ch.scale=source.scale*ratio;chars.Add(ch);
  }
  result.characterTable=chars;result.glyphTable=glyphs;result.fallbackFontAssetTable=new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();result.ReadFontAssetDefinition();result.name="VCArabicSymbols_"+original.GetInstanceID().ToString().Replace('-','N');result.hashCode=TMP_TextUtilities.GetHashCode(result.name);MaterialReferenceManager.AddFontAsset(result);SymbolAliases[original.name]=result.name;UnityEngine.Object.DontDestroyOnLoad(result);return result;
 }
 static State Remember(TMP_Text t){return new State{Font=t.font,Mat=t.fontSharedMaterial,Rtl=t.isRightToLeftText,Auto=t.enableAutoSizing,Size=t.fontSize,Min=t.fontSizeMin,Max=t.fontSizeMax};}
 static void FitText(TMP_Text t,State st){if(!(t is TextMeshProUGUI)||st.Size<=0)return;t.enableAutoSizing=true;t.fontSizeMax=st.Size;t.fontSizeMin=Math.Max(8f,st.Size*.65f);}
 static void Restore(TMP_Text t,State st,bool restoreRtl=true){st.Restoring=true;try{if(restoreRtl)t.isRightToLeftText=st.Rtl;if(t.font==st.ActiveFont&&st.Font!=null){t.font=st.Font;t.fontSharedMaterial=st.Mat;}t.enableAutoSizing=st.Auto;t.fontSizeMin=st.Min;t.fontSizeMax=st.Max;}finally{st.Restoring=false;}}
 static void CardUpdatePostfix(CardView __instance){try{if(__instance==null||CardTraceTotal>=40)return;int id=__instance.GetInstanceID();CardTraceCounts.TryGetValue(id,out int count);if(count>=120)return;var title=__instance._cardNameText;var body=__instance._descriptionText;if(title==null||body==null||!HasArabic(title.OriginalText)&&!HasArabic(body.OriginalText))return;CardTraceCounts[id]=++count;if(count!=1&&count!=120)return;CardTraceTotal++;Self.Log.LogInfo("Card display "+id+" sample="+count+" name="+CardTextStatus(title)+" body="+CardTextStatus(body));}catch(Exception e){if(!Failed){Failed=true;Self.Log.LogError(e);}}}
 static string CardTextStatus(RTLTextMeshPro t){return "'"+(t.OriginalText??"").Replace('\n',' ').Replace('\r',' ').Substring(0,Math.Min(512,(t.OriginalText??"").Length))+"' max="+t.maxVisibleCharacters+" chars="+t.textInfo.characterCount+" font="+(t.font==null?"null":t.font.name)+" material="+(t.fontSharedMaterial==null?"null":t.fontSharedMaterial.name);}
 static bool HasArabic(string s){if(s==null)return false;foreach(char c in s)if((c>=0x600&&c<=0x6ff)||(c>=0xfe70&&c<=0xfeff))return true;return false;}
 internal static TMP_FontAsset GetFont(){
  if(Font!=null)return Font;
  Font=TMP_FontAsset.CreateFontAsset(Path.Combine(Path.GetDirectoryName(Self.GetType().Assembly.Location),"Arabic Pixel 2.ttf"),0,64,8,GlyphRenderMode.SDFAA,2048,2048);
  if(Font==null)throw new Exception("Pixel font creation returned null");
  Font.name="Vampire Crawlers Arabic Pixel 2";
  var chars=new System.Text.StringBuilder();for(int c=0x600;c<=0x6ff;c++)chars.Append((char)c);for(int c=0xfe80;c<=0xfefc;c++)chars.Append((char)c);
  Font.TryAddCharacters(chars.ToString(),out string missing,false);
  Font.atlasPopulationMode=AtlasPopulationMode.Static;
  var mat=Font.material;var shader=Shader.Find("TextMeshPro/Distance Field");if(shader!=null)mat.shader=shader;
  mat.SetFloat("_FaceDilate",.16f);mat.SetColor("_OutlineColor",Color.black);mat.SetFloat("_OutlineWidth",.12f);mat.SetFloat("_OutlineSoftness",0);mat.EnableKeyword("OUTLINE_ON");mat.EnableKeyword("UNDERLAY_ON");mat.SetColor("_UnderlayColor",new Color(0,0,0,.8f));mat.SetFloat("_UnderlayOffsetX",.55f);mat.SetFloat("_UnderlayOffsetY",-.55f);mat.SetFloat("_UnderlayDilate",.08f);mat.SetFloat("_UnderlaySoftness",0);
  UnityEngine.Object.DontDestroyOnLoad(Font);Self.Log.LogInfo("Pixel font created");return Font;
 }
 internal static string SplitLamAlef(string raw){if(string.IsNullOrEmpty(raw))return raw;var output=new System.Text.StringBuilder();foreach(char c in raw){if(c>=0xfef5&&c<=0xfefc){output.Append(c%2==0?'\ufee0':'\ufedf');output.Append(c<=0xfef6?'\ufe82':c<=0xfef8?'\ufe84':c<=0xfefa?'\ufe88':'\ufe8e');}else output.Append(c);}return output.ToString();}
 internal static string Shape(string s){s=NormalizeInlineNumbers(s);if(Cache.TryGetValue(s,out string result))return result;var b=new FastStringBuilder(1024);RTLSupport.FixRTL(s,b,false,true,true);b.Reverse();result=SplitLamAlef(b.ToString());if(Cache.Count>12000)Cache.Clear();Cache[s]=result;return result;}
 static void TextPrefix(TMP_Text __instance,ref string __0){try{if(__instance is RTLTextMeshPro || __instance is RTLTextMeshPro3D){if(HasArabic(__0))GetFontFor(__instance.font);__0=NormalizeInlineNumbers(SplitLamAlef(__0));return;}Process(__instance,ref __0);}catch(Exception e){if(!Failed){Failed=true;Self.Log.LogError(e);}}}
 static void TextPostfix(TMP_Text __instance){try{if(__instance==null||__instance is RTLTextMeshPro||__instance is RTLTextMeshPro3D)return;if(States.TryGetValue(__instance.GetInstanceID(),out State st)&&st.NeedsRefresh){st.NeedsRefresh=false;__instance.ForceMeshUpdate(true,true);}}catch(Exception e){if(!Failed){Failed=true;Self.Log.LogError(e);}}}
 static void NumericTextPostfix(TMP_Text __instance){try{if(__instance==null||__instance is RTLTextMeshPro||__instance is RTLTextMeshPro3D)return;string rendered=__instance.text;if(rendered!=null&&(HasArabic(rendered)||States.ContainsKey(__instance.GetInstanceID())))__instance.text=rendered;}catch(Exception e){if(!Failed){Failed=true;Self.Log.LogError(e);}}}
 static void Process(TMP_Text t,ref string s){if(t==null)return;int id=t.GetInstanceID();bool tracked=States.TryGetValue(id,out State state);
  if(tracked&&(s==state.Input||s==state.Output)&&t.font==state.ActiveFont&&t.isRightToLeftText){s=state.Output;return;}
  if(!HasArabic(s)){if(tracked){Restore(t,state);States.Remove(id);}return;}
  if(!tracked){state=Remember(t);States[id]=state;}var f=GetFontFor(state.Font);state.ActiveFont=f;
  if(t.font!=f){t.font=f;t.fontSharedMaterial=state.Mat==null?f.material:TMP_MaterialManager.GetFallbackMaterial(state.Mat,f.material);t.UpdateMeshPadding();}t.isRightToLeftText=true;FitText(t,state);
  if(s==state.Output)return;state.Input=s;state.Output=Shape(s);state.NeedsRefresh=true;s=state.Output;
 }
 static void TextGetter(TMP_Text __instance,ref string __result){if(__instance!=null&&States.TryGetValue(__instance.GetInstanceID(),out State s)&&s.Output==__result)__result=s.Input;}
}
}
