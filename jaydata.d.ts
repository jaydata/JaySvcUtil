module $data {
    interface IPromise {
        then: (handler: ( args: any) => IPromise ) => IPromise;
        fail: (handler: ( args: any) => IPromise ) => IPromise;
    };

    class Base {
        getType: () => Function;
    };

    interface Event { 
        attach(eventHandler: (sender: any, event: any) => void ): void;
        detach(eventHandler: () => void): void;
        fire(e: any, sender: any): void;
    }

    class Entity extends Base {
        entityState: number;
        changedProperties: Array;

        propertyChanging: Event;
        propertyChanged: Event;
        propertyValidationError: Event;
        isValid: bool;
    };

    interface EntitySet {
        tableName: string;
        collectionName: string;
        
        add(initData: { }): Entity;
        add(item: Entity): Entity;

        attach(item: Entity): void;
        attach(item: { }): void;
        attachOrGet(item: Entity): Entity;
        attachOrGet(item: { }): Entity;

        detach(item: Entity): void;
        detach(item: { }): void;

        remove(item: Entity ): void;
        remove(item: { }): void;

        elementType: new () => Entity;
    }

    interface Queryable {
        filter(predicate:(it: any) => bool): Queryable;
        filter(predicate:(it: any) => bool, thisArg: any): Queryable;

        map(projection: (it: any) => any): Queryable;

        length(): $data.IPromise;
        length(handler: (result: number) => void): $data.IPromise;
        length(handler: { success?: (result: number) => void; error?: (result: any) => void; }): $data.IPromise;

        forEach(handler: (it: any) => void ): $data.IPromise;
    
        toArray(): $data.IPromise;
        toArray(handler: (result: any[]) => void): $data.IPromise;
        toArray(handler: { success?: (result: any[]) => void; error?: (result: any) => void; }): $data.IPromise;

        single(predicate: (it: any, params?: any) => bool, params?: any, handler?: (result: any) => void): $data.IPromise;
        single(predicate: (it: any, params?: any) => bool, params?: any, handler?: { success?: (result: any[]) => void; error?: (result: any) => void; }): $data.IPromise;

        take(amout: number): Queryable;
        skip(amout: number): Queryable;

        order(selector: string): Queryable;
        orderBy(predicate: (it: any) => any): Queryable;
        orderByDescending(predicate: (it: any) => any): Queryable;
    
        first(predicate: (it: any, params?: any) => bool, params?: any, handler?: (result: any) => void): $data.IPromise;
        first(predicate: (it: any, params?: any) => bool, params?: any, handler?: { success?: (result: any[]) => void; error?: (result: any) => void; }): $data.IPromise;
    
        include(selector: string): Queryable;

        removeAll(): $data.IPromise;
        removeAll(handler: (count: number) => void): $data.IPromise;
        removeAll(handler: { success?: (result: number) => void; error?: (result: any) => void; }): $data.IPromise;
    }

    class EntityContext {
        constructor (config: { name: string; oDataServiceHost?: string; databaseName?: string; localStoreName?: string; user?: string; password?: string; });

        onReady(handler: (context: EntityContext) => void): $data.IPromise;
        saveChanges(): $data.IPromise;
        saveChanges(handler: (result: number) => void ): $data.IPromise;
        saveChanges(cb: { success: (result: number) => void; error: (result: any) => void; }): $data.IPromise;
    }

    export class Blob {
    
    };
    export class Guid {
        constructor (value: string);
        value: string;
    };

    export class Geography {
        constructor (longitude: number, latitude: number);
        longitude: number;
        latitude: number;
    };

};


