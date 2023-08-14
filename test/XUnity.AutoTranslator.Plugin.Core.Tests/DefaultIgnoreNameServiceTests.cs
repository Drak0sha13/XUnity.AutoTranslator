using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using XUnity.AutoTranslator.Plugin.Core.Services;

namespace XUnity.AutoTranslator.Plugin.Core.Tests
{
   public class DefaultIgnoreNameServiceTests
   {
      private readonly IIgnoreNameService service;

      public DefaultIgnoreNameServiceTests()
      {
         service = new DefaultIgnoreNameService();
         service.Add( "ItemList/*/Item*" );
         service.Add( "FriendList/*Item*" );
      }

      [ Theory ]
      [InlineData( "Item", false )]
      [InlineData( "Item1", false )]
      [InlineData( "1Item1", false )]
      [InlineData( "1Item", false )]
      [InlineData( "ItemList/Item", false )]
      [InlineData( "ItemList/Item1", false)]
      [InlineData( "ItemList/ii/Item", true)]
      [InlineData( "ItemList/ii/Item1", true)]
      [InlineData( "ItemList/1Item", false)]
      [InlineData( "ItemList/1Item1", false)]
      [InlineData( "ItemList/ii/1Item1", false)]
      [InlineData( "ItemList/ii/Itm", false)]

      [InlineData( "FriendList/Item", true)]
      [InlineData( "FriendList/Item1", true )]
      [InlineData( "FriendList/ii/Item", false )]
      [InlineData( "FriendList/ii/Item1", false)]
      [InlineData( "FriendList/1Item", true )]
      [InlineData( "FriendList/1Item1", true )]
      [InlineData( "FriendList/ii/1Item1", false)]
      [InlineData( "FriendList/ii/Itm", false)]
      public void Ignore( string name, bool valid )
      {
         var reverseName = name.Split( '/' ).Reverse().ToList();
         Assert.Equal( valid, service.HasIgnoredName( reverseName ) );
      }
   }

   public class DefaultIgnoreNameServiceTests_Init
   {
      [ Theory ]
      [ InlineData( "Item", IgnoreItemEnum.Equals, true ) ]
      [ InlineData( "Item*", IgnoreItemEnum.Start, true ) ]
      [ InlineData( "*Item", IgnoreItemEnum.End, true ) ]
      [ InlineData( "It*em", IgnoreItemEnum.StartEnd, true ) ]
      [ InlineData( "Item**", IgnoreItemEnum.Custom, false ) ]
      [ InlineData( "I*tem*", IgnoreItemEnum.Custom, false ) ]
      [ InlineData( "*Item*", IgnoreItemEnum.Contains, true ) ]
      [ InlineData( "*", IgnoreItemEnum.Any, false ) ]
      [ InlineData( "***Item*", IgnoreItemEnum.Custom, false ) ]
      [ InlineData( "*/*", IgnoreItemEnum.Custom, false ) ]
      [ InlineData( "*/Item", IgnoreItemEnum.Equals, true ) ]
      [ InlineData( "*/Item*", IgnoreItemEnum.Start, true ) ]
      [ InlineData( "*/*Item", IgnoreItemEnum.End, true )]
      [ InlineData( "*/Item**", IgnoreItemEnum.Custom, false ) ]
      [ InlineData( "*/I*tem*", IgnoreItemEnum.Custom, false ) ]
      [ InlineData( "*/*Item*", IgnoreItemEnum.Contains, true ) ]
      [ InlineData( "*/***Item*", IgnoreItemEnum.Custom, false ) ]
      public void Init_Simple( string name, IgnoreItemEnum type, bool valid )
      {
         IIgnoreNameService service = new DefaultIgnoreNameService();
         bool v = service.Add( name );
         Assert.Equal( valid, v );
         if( valid )
         {
            Assert.Collection( service,
               x =>
               {
                  Assert.Equal( type, x.TypeSearch );
                  Assert.True( x.IsLeaf, $"'{name}' is not leaf" );
               } );
         }
         else
            Assert.Empty( service );
      }

      [Theory]
      [InlineData( "ItemList/Item", true )]
      [InlineData( "ItemList/Item*", true )]
      [InlineData( "ItemList/Item**", false )]
      [InlineData( "ItemList/I*tem*", false )]
      [InlineData( "ItemList/*Item*", true )]
      [InlineData( "ItemList/*", true )]
      [InlineData( "ItemList/***Item*", false )]
      [InlineData( "ItemList*/Item", true )]
      [InlineData( "ItemList**/Item", false )]
      [InlineData( "I*temList*/Item", false )]
      [InlineData( "*ItemList*/Item", true )]
      [InlineData( "***ItemList*/Item", false )]
      public void Init_Second( string name, bool valid )
      {
         IIgnoreNameService service = new DefaultIgnoreNameService();
         bool v = service.Add( name );
         Assert.Equal( valid, v );
         if( !valid )
         {
            Assert.Empty( service );
            return;
         }
         Assert.Single( service );
         Assert.Collection( service,
            x =>
            {
               Assert.False( x.IsLeaf, $"'{name}' is leaf" );
               Assert.Collection( x,
                  y =>
                  {
                     Assert.True( y.IsLeaf, $"'{name}' is not leaf" );
                  } );
            } );
      }

      public static IEnumerable<object[]> Init_Merge2_Data()
      {
         yield return new object[] { new[] { "ItemList/Item", "ItemList/Item" }, new [] { 1 }  };
         yield return new object[] { new[] { "ItemList/Item", "ItemList/Item1" }, new[] { 1 , 1 } };
         yield return new object[] { new[] { "ItemList/Item", "ItemList*/Item" }, new[] { 2 } };
         yield return new object[] { new[] { "ItemList/Item", "List/ItemList/Item" }, new[] { 1 } };
         yield return new object[] { new[] { "List/ItemList/Item", "ItemList/Item" }, new[] { 1 } };
      }

      [Theory]
      [MemberData( nameof(Init_Merge2_Data) )]
      public void Init_Merge2( string[] names, int[] count )
      {
         IIgnoreNameService service = new DefaultIgnoreNameService();
         foreach( string name in names )
         {
            service.Add( name );
         }

         EqualsTreeCount( service, count );
      }

      void EqualsListCount( ICollection<IgnoreItem> items, int count )
      {
         Assert.Equal( count, items.Count );
         foreach( IgnoreItem item in items )
         {
            Assert.Empty( item );
            Assert.Equal( true, item.IsLeaf );
         }
      }

      void EqualsTreeCount( ICollection<IgnoreItem> items, IEnumerable count )
      {
         Assert.Equal( count.Cast<object>().Count(), items.Count );
         var j = items.Zip( count.Cast<object>(),
            ( x, y ) =>
            {
               switch( y )
               {
                  case IEnumerable enumerable: EqualsTreeCount( x, enumerable ); break;
                  case int i: EqualsListCount( x, i ); break;
               }

               return 1;
            } ).Count();
      }
   }
}
