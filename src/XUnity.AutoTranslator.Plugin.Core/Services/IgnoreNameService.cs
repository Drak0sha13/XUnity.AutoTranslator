using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XUnity.AutoTranslator.Plugin.Core.Extensions;
using XUnity.Common.Logging;

namespace XUnity.AutoTranslator.Plugin.Core.Services
{
   public class DefaultIgnoreNameService : IIgnoreNameService
   {
      public const string IgnoreTranslation = "IgnoreTranslation";
      private static IIgnoreNameService instance;

      /// <summary> Need using trie for speed </summary>
      protected IgnoreItem Root { get; } = new IgnoreItem( IgnoreItemEnum.Any, null, null );

      public static IIgnoreNameService Instance
      {
         get => instance ??= new DefaultIgnoreNameService();
         set => instance = value;
      }

      public Dictionary<IgnoreItemEnum, Func<string, string, string, bool>> ListFunctions { get; }
         = new()
         {
            { IgnoreItemEnum.Equals, DefaultIgnoreNameActions.Equals },
            { IgnoreItemEnum.Contains, DefaultIgnoreNameActions.Contains },
            { IgnoreItemEnum.Any, DefaultIgnoreNameActions.Any },
            { IgnoreItemEnum.Start, DefaultIgnoreNameActions.Start },
            { IgnoreItemEnum.End, DefaultIgnoreNameActions.End },
            { IgnoreItemEnum.StartEnd, DefaultIgnoreNameActions.StartEnd },
         };


      public virtual void Init()
      {
         try
         {
            var dataFolder = Path.Combine( PluginEnvironment.Current.TranslationPath, IgnoreTranslation );
            DirectoryInfo dir = new DirectoryInfo( dataFolder );
            dir.Create();
            foreach( var fileInfo in dir.GetFiles( "*.txt" ,SearchOption.TopDirectoryOnly ) )
            {
               try
               {
                  using var stream = fileInfo.OpenRead();
                  Init(stream);
               }
               catch( Exception ex )
               {
                  Warn( ex, $"Error in file '{fileInfo.FullName}'" );
               }
            }
         }
         catch( Exception ex )
         {
            Warn( ex, $"Error in directory '{IgnoreTranslation}'" );
         }
      }

      public virtual void Init( Stream stream )
      {
         using StreamReader sr = new StreamReader(stream);
         while( !sr.EndOfStream )
         {
            try
            {
               var readLine = sr.ReadLine();
               if( readLine == null )
                  continue;
               var line = readLine.Trim();
               if( line.Length == 0)
                  continue;
               if( line[0] == '#' )
                  continue;
               if( !Add( line ))
                  Warn( null, $"Not added line '{readLine}'" );
            }
            catch( Exception ex )
            {
               if( stream is FileStream fileStream )
                  Warn( ex, $"Error in position {stream.Position} in file '{fileStream.Name}'" );
               else
                  Warn( ex, $"Error in position {stream.Position}" );
            }
         }
      }

      /// <summary> "FriendList/Item*" </summary>
      /// <param name="name"></param>
      /// <returns></returns>
      public virtual bool Add( string name )
      {
         if(name == null)
            return false;
         return Add( name.Split( new []{'/'}, StringSplitOptions.RemoveEmptyEntries ) );
      }

      /// <summary> "FriendList", "Item*" </summary>
      /// <param name="names"></param>
      /// <returns></returns>
      protected virtual bool Add( string[] names )
      {
         if( names == null || names.Length == 0 )
            return false;

         var parent = new IgnoreItem( IgnoreItemEnum.Any, null, null, DefaultIgnoreNameActions.Any );
         var temp = parent;
         for( int i = names.Length - 1; i >= 0; i-- )
         {
            string name = names[i];
            var item = CreateIgnoreItem( name );
            if( item == null )
               return false;
            temp.Add( item );
            temp = item;
         }
         temp.IsLeaf = true;

         return Root.AddRange( parent );
      }

      protected virtual IgnoreItem CreateIgnoreItem( string name )
      {
         if( name == null)
            return null;
         var strings = name.Split( new char[] { '*' } );
         switch( strings.Length )
         {
            case 1:
               if( !String.IsNullOrEmpty( strings[ 0 ] ) )
                  return CreateIgnoreItem( IgnoreItemEnum.Equals, strings[0], null );
               break;
            case 2:
               string s1 = strings[ 0 ];
               string s2 = strings[ 1 ];
               var emptyS1 = String.IsNullOrEmpty( s1 );
               var emptyS2 = String.IsNullOrEmpty( s2 );
               return emptyS1 switch
               {
                  true when emptyS2 => CreateIgnoreItem( IgnoreItemEnum.Any, s1, s2 ),
                  true => CreateIgnoreItem( IgnoreItemEnum.End, s1, s2 ),
                  false when !emptyS2 => CreateIgnoreItem( IgnoreItemEnum.StartEnd, s1, s2 ),
                  false => CreateIgnoreItem( IgnoreItemEnum.Start, s1, s2 )
               };
            case 3:
               if( String.IsNullOrEmpty(strings[0])
                   && !String.IsNullOrEmpty( strings[ 1 ] )
                   && String.IsNullOrEmpty( strings[ 2 ] ) )
                  return CreateIgnoreItem( IgnoreItemEnum.Contains, strings[1], null );
               break;
         }

         return null;
      }

      protected virtual IgnoreItem CreateIgnoreItem( IgnoreItemEnum typeSearch, string start, string end )
      {
         var action = CreateAction(typeSearch, start, end );
         return new IgnoreItem( typeSearch, start, end, action );
      }

      private Func<string, string, string, bool> CreateAction( IgnoreItemEnum typeSearch, string start, string end )
      {
         if( !ListFunctions.TryGetValue( typeSearch, out var action ) )
         {
            Warn( null, $"IgnoreItemEnum '{typeSearch} not found'" );
            return DefaultIgnoreNameActions.Any;
         }
         return action;
      }

      protected virtual void Warn( Exception ex, string msg)
      {
         if( ex == null )
            XuaLogger.AutoTranslator.Warn( msg );
         else
            XuaLogger.AutoTranslator.Error( ex, msg );
      }

      public virtual bool HasIgnoredName( IEnumerable<string> names )
      {
         // can be a field of the class if executed in a single thread
         var list1 = new List<IgnoreItem>(100);
         var list2 = new List<IgnoreItem>( 100 );
         list1.AddRange( Root );
         foreach( var name in names )
         {
            foreach( IgnoreItem item in list1 )
            {
               bool b = item.HasIgnoredName( name );
               if(!b)
                  continue;
               
               if( item.IsLeaf )
                  return true;

               list2.AddRange( item );
            }

            if( list2.Count == 0 )
               return false;

            list1.Clear();
            var t = list2;
            list2 = list1;
            list1 = t;
         }
         return false;
      }

      /// <inheritdoc />
      public IEnumerator<IgnoreItem> GetEnumerator()
      {
         return Root.GetEnumerator();
      }

      /// <inheritdoc />
      IEnumerator IEnumerable.GetEnumerator()
      {
         return GetEnumerator();
      }

      /// <inheritdoc />
      void ICollection<IgnoreItem>.Add( IgnoreItem item )
      {
         if( !Root.Add( item ) )
            throw new NotSupportedException($"{item} not support");
      }

      /// <inheritdoc />
      public void Clear()
      {
         Root.Clear();
      }

      /// <inheritdoc />
      public bool Contains( IgnoreItem item )
      {
         return Root.Contains( item );
      }

      /// <inheritdoc />
      public void CopyTo( IgnoreItem[] array, int arrayIndex )
      {
         Root.CopyTo( array, arrayIndex );
      }

      /// <inheritdoc />
      public bool Remove( IgnoreItem item )
      {
         return Root.Remove( item );
      }

      /// <inheritdoc />
      public int Count => Root.Count;

      /// <inheritdoc />
      public bool IsReadOnly => false;
   }

   public class IgnoreItem : ICollection<IgnoreItem>, IEquatable<IgnoreItem>, ICloneable
   {
      private bool isLeaf;

      public IgnoreItem( IgnoreItemEnum typeSearch,
         string start,
         string end,
         Func<string, string, string, bool> action = null )
      {
         TypeSearch = typeSearch;
         Start = start;
         End = end;
         Action = action ?? DefaultIgnoreNameActions.Any;
      }

      public IgnoreItem( IgnoreItem item ) : this(item.TypeSearch, item.Start, item.End, item.Action)
      {
         IsLeaf = item.IsLeaf;
      }

      public string Start { get; }
      public string End { get; }
      public Func<string, string, string, bool> Action { get; }
      protected List<IgnoreItem> List { get; } = new List<IgnoreItem>();
      public IgnoreItemEnum TypeSearch { get; }

      public bool IsLeaf
      {
         get => isLeaf;
         set
         {
            isLeaf = value;
            if( isLeaf )
               List.Clear();
         }
      }

      public virtual bool Add( IgnoreItem item )
      {
         if( item == null )
            return false;
         if( IsLeaf )
            return false;
         if( Optimize( item ) != true )
            return false;

         Merge( item );
         return true;

      }

      public bool HasIgnoredName( string name )
      {
         return Action( Start, End, name );
      }

      /// <inheritdoc />
      public void Clear()
      {
         List.Clear();
      }

      /// <inheritdoc />
      public bool Contains( IgnoreItem item )
      {
         return List.Contains( item );
      }

      /// <inheritdoc />
      public void CopyTo( IgnoreItem[] array, int arrayIndex )
      {
         List.CopyTo( array, arrayIndex);
      }

      protected virtual void Merge( IgnoreItem item )
      {
         Merge( this, item );
      }

      public static void Merge( IgnoreItem dst, IgnoreItem src )
      {
         if( dst == null || src == null)
            return;
         if(dst.IsLeaf )
            return;
         var indexOf = dst.List.IndexOf( src );
         if( indexOf < 0 )
         {
            dst.List.Add( src );
         }
         else
         {
            var t = dst.List[ indexOf ];
            foreach (var item in src.List)
               Merge( t, item );
            if(src.IsLeaf)
               t.IsLeaf = true;
         }
      }

      protected virtual bool? Optimize( IgnoreItem item )
      {
         if(item == null)
            return null;

         if( !item.IsLeaf )
         {
            // it is not optimized
            List<IgnoreItem> list = null;
            foreach( var i in item )
            {
               bool? b = Optimize( i );
               if( b == null )
                  return null;
               if( b == false )
               {
                  list ??= new List<IgnoreItem>();
                  list.Add( i );
               }
            }

            if( list != null )
               foreach( var i in list )
                  item.Remove( i );
         }

         if( item.IsLeaf )
         {
            if( item.TypeSearch == IgnoreItemEnum.Any )
               return false;
         }
         return true;
      }

      public virtual bool AddRange( IEnumerable<IgnoreItem> items )
      {
         if( items == null )
            return false;
         bool ret = false;
         foreach( var item in items )
            ret |= Add( item );
         return ret;
      }

      /// <inheritdoc />
      void ICollection<IgnoreItem>.Add( IgnoreItem item )
      {
         Add( item );
      }

      public bool Remove( IgnoreItem item )
      {
         bool remove = List.Remove( item );
         if( List.Count == 0 )
            IsLeaf = true;
         return remove;
      }

      /// <inheritdoc />
      public int Count => List.Count;

      /// <inheritdoc />
      public bool IsReadOnly => IsLeaf;

      /// <inheritdoc />
      public bool Equals(IgnoreItem other)
      {
         if (ReferenceEquals(null, other))
            return false;
         if (ReferenceEquals(this, other))
            return true;
         return string.Equals(Start, other.Start, StringComparison.OrdinalIgnoreCase)
                && string.Equals(End, other.End, StringComparison.OrdinalIgnoreCase)
                && TypeSearch == other.TypeSearch;
      }

      /// <inheritdoc />
      public IEnumerator<IgnoreItem> GetEnumerator()
      {
         return List.GetEnumerator();
      }

      /// <inheritdoc />
      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj))
            return false;
         if (ReferenceEquals(this, obj))
            return true;
         if (obj.GetType() != this.GetType())
            return false;
         return Equals((IgnoreItem)obj);
      }

      /// <inheritdoc />
      public override int GetHashCode()
      {
         unchecked
         {
            var hashCode = (Start != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Start) : 0);
            hashCode = (hashCode * 397) ^ (End != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(End) : 0);
            hashCode = (hashCode * 397) ^ (int)TypeSearch;
            return hashCode;
         }
      }

      public object Clone()
      {
         return new IgnoreItem( this );
      }

      /// <inheritdoc />
      IEnumerator IEnumerable.GetEnumerator()
      {
         return GetEnumerator();
      }

      public static bool operator ==(IgnoreItem left, IgnoreItem right)
      {
         return Equals(left, right);
      }

      public static bool operator !=(IgnoreItem left, IgnoreItem right)
      {
         return !Equals(left, right);
      }

      /// <inheritdoc />
      public override string ToString()
      {
         return TypeSearch switch
         {
            IgnoreItemEnum.Equals => $"{Start}{End}",
            IgnoreItemEnum.Contains => $"*{Start}{End}*",
            IgnoreItemEnum.Any => $"*",
            IgnoreItemEnum.Start => $"{Start}{End}*",
            IgnoreItemEnum.End => $"*{Start}{End}",
            IgnoreItemEnum.StartEnd => $"{Start}*{End}",
            IgnoreItemEnum.Custom => $"**{Start}**{End}**",
            _ => $"**{Start}**{End}**"
         };
      }
   }

   public enum IgnoreItemEnum
   {
      /// <summary> "Item" </summary>
      Equals,

      /// <summary> "*Item*" </summary>
      Contains,

      /// <summary> "*" </summary>
      Any,

      /// <summary> "*Item" </summary>
      Start,

      /// <summary> "Item*" </summary>
      End,

      /// <summary> "Item*Item" </summary>
      StartEnd,

      /// <summary> Unknown </summary>
      Custom,
   }

   public interface IIgnoreNameService : ICollection<IgnoreItem>
   {
      void Init();
      void Init( Stream stream );
      bool Add( string name );
      bool HasIgnoredName( IEnumerable<string> names );
   }

   public static class DefaultIgnoreNameActions
   {
      /// <summary> "Item" </summary>
      public static bool Equals( string start, string end, string value )
      {
         return value.Equals( start, StringComparison.InvariantCultureIgnoreCase );
      }

      /// <summary> "*Item*" </summary>
      public static bool Contains( string start, string end, string value )
      {
         return value.IndexOf( start, StringComparison.InvariantCultureIgnoreCase ) >= 0;
      }

      /// <summary> "*" </summary>
      public static bool Any( string start, string end, string value )
      {
         return true;
      }

      /// <summary> "*Item" </summary>
      public static bool Start( string start, string end, string value )
      {
         return value.StartsWith( start, StringComparison.InvariantCultureIgnoreCase );
      }

      /// <summary> "Item*" </summary>
      public static bool End( string start, string end, string value )
      {
         return value.EndsWith( end, StringComparison.InvariantCultureIgnoreCase );
      }

      /// <summary> "Item*Item" </summary>
      public static bool StartEnd( string start, string end, string value )
      {
         return Start( start, end, value )
                && End( start, end, value.Substring( start.Length ) );
      }
   }
}
