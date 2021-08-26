'use strict';

const indentMark='  ';
const openMark='{'
const closeMark='}'
const eolMark='\n';

function dumpObject(object, indentLevel=1)
{
  if(!object)
    return `'${object}'`;
  
  const indent = indentMark.repeat(indentLevel);
  let poo = openMark;
  
  for(const [key, value] of Object.entries(object)) 
  {
    const valueType = typeof(value);
    
    poo += `${eolMark}${indent}[${valueType}] ${key}: `;
    
    if(valueType=="object")
    { 
      // Own property? Recurse it.
      // Inherited? tread as value without inquisitioning it's content.
      if(object.hasOwnProperty(key))
      {
        poo += dumpObject(value,indentLevel+1);
        continue;
      }
    }
    else if(valueType=="function")
    {
      // don't output function body, 'defined' if present
      if(value)
      {
        poo += "'defined'";
        continue;
      }
    }
    
    poo += `'${value}'`;
  }

  poo += eolMark + indentMark.repeat(indentLevel-1) + closeMark;
  return poo;
}

export const Debug = 
{
  stringify: function(obj2str)
  {
    return dumpObject(obj2str,1);
  },

  logLineByLine: function(str, separator ='\n')
  {
    const lineArr = str.split(separator);
    for( line of lineArr )
      console.debug(line);
  }
};