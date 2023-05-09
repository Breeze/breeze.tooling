import { ComplexType, ComplexAspect, ComplexObject } from 'breeze-client';

export class BaseComplex implements ComplexObject {
  complexAspect: ComplexAspect;
  complexType: ComplexType;
}
