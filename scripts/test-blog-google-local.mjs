// Checks only the loopback API; never prints credentials, OAuth state or cookies.
import assert from 'node:assert/strict';
const base=process.env.BLOG_TEST_API_URL||'http://localhost:5031';
if(!['http://localhost:5031','http://localhost:4173'].includes(base))throw new Error('Only the local News environments are allowed.');
let passed=0;
async function request(path,options={}){
    const response=await fetch(base+path,{redirect:'manual',...options});passed++;return response;
}
const session=await (await request('/api/blog/auth/session')).json();
assert.equal(session.user,null);assert.equal(session.providers.google,true);assert.equal(session.providers.facebook,false);
const login=await request('/api/blog/auth/start/google?returnUrl='+encodeURIComponent(base+'/blog/pt/autor-b7c99e8b'));
assert.equal(login.status,302);
const google=new URL(login.headers.get('location'));
assert.equal(google.hostname,'accounts.google.com');
assert.equal(google.searchParams.get('redirect_uri'),base+'/api/blog/auth/callback/google');
assert.equal(google.searchParams.get('response_type'),'code');
assert.equal(google.searchParams.get('scope'),'profile');
assert.equal(google.searchParams.get('code_challenge_method'),'S256');
assert.ok(google.searchParams.get('code_challenge'));
assert.ok(google.searchParams.get('state'));
assert.ok(google.searchParams.get('client_id')?.endsWith('.apps.googleusercontent.com'));
assert.ok(login.headers.getSetCookie().some(cookie=>cookie.includes('.AspNetCore.Correlation.')&&cookie.toLowerCase().includes('httponly')));
assert.equal((await request('/api/blog/auth/start/google?returnUrl=https%3A%2F%2Fevil.example%2Fblog')).status,400);
assert.equal((await request('/api/blog/auth/test',{method:'POST',headers:{'X-Crica-Interaction':'1'}})).status,404);
const failure=await request('/api/blog/auth/callback/google?error=access_denied');
assert.equal(failure.status,302);assert.ok(failure.headers.get('location')?.includes('blogLogin=failed'));
assert.equal((await (await request('/api/blog/auth/session')).json()).user,null);
console.log(JSON.stringify({passed,googleEnabled:true,facebookEnabled:false,pkce:true,testLoginDisabled:true,callback:base+'/api/blog/auth/callback/google'}));
