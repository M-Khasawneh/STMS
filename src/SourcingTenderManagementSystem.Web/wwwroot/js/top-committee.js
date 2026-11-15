'use strict';
document.querySelectorAll('[data-top-members]').forEach(form=>{const fields=[...form.querySelectorAll('select')];const validate=()=>{for(const field of fields)field.setCustomValidity(field.value&&fields.filter(x=>x.value===field.value).length>1?'Choose four different employees.':'');};form.addEventListener('change',validate);validate();});
