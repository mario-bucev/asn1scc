-- Code automatically generated by asn1scc tool
WITH adaasn1rtl;
WITH uper_asn1_rtl;
WITH acn_asn1_rtl;
WITH D;
use type adaasn1rtl.Asn1UInt;
use type adaasn1rtl.Asn1Int;
use type adaasn1rtl.BIT;
use D;

PACKAGE BODY postencoding with SPARK_Mode IS



   procedure my_encoding_patcher(val:in T_Packet; bitStreamPositions_start1 : adaasn1rtl.BitStreamPtr; bitStreamPositions_1:T_Packet_extension_function_positions; bs : in out adaasn1rtl.Bitstream)
   is
   begin
      null;
   end;
   

   FUNCTION crc_validator(val:in T_Packet; bitStreamPositions_start1 : adaasn1rtl.BitStreamPtr; bitStreamPositions_1:T_Packet_extension_function_positions; bs : in out adaasn1rtl.Bitstream) return adaasn1rtl.ASN1_RESULT
   is 
   begin
      return adaasn1rtl.ASN1_RESULT'(Success => True, ErrorCode => 0);
   end;
      



 

END postencoding;
